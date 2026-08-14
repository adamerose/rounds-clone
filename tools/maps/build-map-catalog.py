#!/usr/bin/env python3
"""Build the clean-room arena catalog from the ignored public workbook export.

The workbook is research input and must remain under research/raw/.  This script
commits only derived geometry and evidence to spec/maps.json.
"""

from __future__ import annotations

import argparse
import hashlib
import heapq
import json
import math
from pathlib import Path
import xml.etree.ElementTree as ET
import zipfile

import numpy as np
from PIL import Image


WIDTH = 640
HEIGHT = 360
PLAYER_DIAMETER_PIXELS = 18.0
MASK_THRESHOLD = 24
MIN_COARSE_IOU = 0.75
MIN_APPROXIMATE_FILL = 0.75
MAX_PRIMITIVES_PER_MAP = 96
COARSE_CELL_PIXELS = 8

DRAWING_NS = {
    "xdr": "http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing",
    "a": "http://schemas.openxmlformats.org/drawingml/2006/main",
    "r": "http://schemas.openxmlformats.org/officeDocument/2006/relationships",
}

# Visible saw centers and radii measured in the anchored 640x360 previews.
SAWS = {
    3: [(320, 180, 43), (320, 347, 43)],
    15: [(220, 79, 42), (320, 112, 42), (420, 80, 42)],
    23: [(320, 180, 42)],
    45: [(320, 164, 43)],
    46: [(208, 162, 43), (432, 162, 43)],
    53: [
        (68, 192, 40), (68, 242, 40), (68, 292, 40), (68, 340, 40),
        (194, 288, 40), (446, 288, 40),
        (572, 192, 40), (572, 242, 40), (572, 292, 40), (572, 340, 40),
    ],
    54: [(125, 52, 42), (515, 53, 42)],
}

# A still preview can identify a visual assembly, but cannot prove its runtime
# behavior.  These remain explicit candidates until controlled footage binds it.
BREAKABLE_CANDIDATES = {16}
MOVING_CANDIDATES = {26}
PHYSICS_CANDIDATES = {30}
RING_OUT_FOCUSED = {6}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "workbook",
        nargs="?",
        default="research/raw/map-correction/maps.xlsx",
        help="Ignored XLSX export of the public map sheet.",
    )
    parser.add_argument("--output", default="spec/maps.json")
    return parser.parse_args()


def workbook_rows(archive: zipfile.ZipFile) -> dict[int, str]:
    drawing = ET.fromstring(archive.read("xl/drawings/drawing1.xml"))
    relationships = ET.fromstring(
        archive.read("xl/drawings/_rels/drawing1.xml.rels")
    )
    targets = {
        relation.attrib["Id"]: relation.attrib["Target"].split("/")[-1]
        for relation in relationships
    }
    rows: dict[int, str] = {}
    embed_name = f"{{{DRAWING_NS['r']}}}embed"
    for anchor in drawing:
        origin = anchor.find("xdr:from", DRAWING_NS)
        blip = anchor.find(".//a:blip", DRAWING_NS)
        if origin is None or blip is None:
            continue
        sheet_row = int(origin.find("xdr:row", DRAWING_NS).text) + 1
        rows[sheet_row] = targets[blip.attrib[embed_name]]
    expected = set(range(2, 72))
    if set(rows) != expected:
        raise ValueError("Workbook must contain exactly one anchored image in rows 2-71.")
    return rows


def source_mask(image_bytes) -> np.ndarray:
    image = np.asarray(Image.open(image_bytes).convert("RGB"))
    if image.shape != (HEIGHT, WIDTH, 3):
        raise ValueError(f"Expected a {WIDTH}x{HEIGHT} preview, got {image.shape}.")
    return image.max(axis=2) >= MASK_THRESHOLD


def connected_components(mask: np.ndarray) -> list[np.ndarray]:
    """Return 8-connected foreground components using deterministic scanline union."""
    runs: list[tuple[int, int, int]] = []
    parents: list[int] = []
    previous: list[int] = []

    def find(item: int) -> int:
        while parents[item] != item:
            parents[item] = parents[parents[item]]
            item = parents[item]
        return item

    def union(left: int, right: int) -> None:
        left_root = find(left)
        right_root = find(right)
        if left_root != right_root:
            parents[right_root] = left_root

    for y in range(HEIGHT):
        foreground = np.flatnonzero(mask[y])
        current: list[int] = []
        if len(foreground):
            cuts = np.flatnonzero(np.diff(foreground) > 1)
            starts = np.r_[0, cuts + 1]
            ends = np.r_[cuts, len(foreground) - 1]
            for start, end in zip(starts, ends):
                x_min = int(foreground[start])
                x_max = int(foreground[end])
                run_id = len(runs)
                runs.append((y, x_min, x_max))
                parents.append(run_id)
                current.append(run_id)
                for previous_id in previous:
                    _, previous_min, previous_max = runs[previous_id]
                    if previous_max >= x_min - 1 and previous_min <= x_max + 1:
                        union(run_id, previous_id)
        previous = current

    groups: dict[int, list[tuple[int, int, int]]] = {}
    for run_id, run in enumerate(runs):
        groups.setdefault(find(run_id), []).append(run)

    components: list[np.ndarray] = []
    for component_runs in groups.values():
        count = sum(x_max - x_min + 1 for _, x_min, x_max in component_runs)
        points = np.empty((count, 2), dtype=np.float64)
        offset = 0
        for y, x_min, x_max in component_runs:
            length = x_max - x_min + 1
            points[offset : offset + length, 0] = np.arange(x_min, x_max + 1) + 0.5
            points[offset : offset + length, 1] = y + 0.5
            offset += length
        components.append(points)
    return components


class Fitter:
    def __init__(self) -> None:
        self.next_id = 0

    def fit(self, points: np.ndarray) -> dict:
        x_values = points[:, 0]
        y_values = points[:, 1]
        angles = [0.0, math.pi / 4, -math.pi / 4]
        if len(points) > 2:
            covariance = np.cov(points.T)
            values, vectors = np.linalg.eigh(covariance)
            vector = vectors[:, np.argmax(values)]
            principal = math.atan2(vector[1], vector[0])
            angles.extend((principal, principal + math.pi / 2))

        best = None
        for angle in angles:
            cosine = math.cos(angle)
            sine = math.sin(angle)
            u = x_values * cosine + y_values * sine
            v = -x_values * sine + y_values * cosine
            # Pixel centers must remain inside after six-decimal world rounding.
            u_min, u_max = float(u.min() - 0.51), float(u.max() + 0.51)
            v_min, v_max = float(v.min() - 0.51), float(v.max() + 0.51)
            area = (u_max - u_min) * (v_max - v_min)
            candidate = (area, angle, u_min, u_max, v_min, v_max)
            if best is None or candidate < best:
                best = candidate

        self.next_id += 1
        return {
            "id": self.next_id,
            "points": points,
            "box": best,
            "loss": best[0] - len(points),
        }

    def split(self, node: dict) -> tuple[dict, dict] | None:
        points = node["points"]
        axes = [np.array((1.0, 0.0)), np.array((0.0, 1.0))]
        if len(points) > 3:
            _, vectors = np.linalg.eigh(np.cov(points.T))
            axes.extend((vectors[:, 0], vectors[:, 1]))

        best = None
        for axis in axes:
            projection = points @ axis
            order = np.argsort(projection, kind="stable")
            ordered = projection[order]
            candidates = {
                int(len(points) * fraction)
                for fraction in (0.15, 0.25, 0.4, 0.5, 0.6, 0.75, 0.85)
            }
            gaps = np.diff(ordered)
            if len(gaps):
                count = min(8, len(gaps))
                candidates.update(
                    int(index + 1)
                    for index in np.argpartition(gaps, -count)[-count:]
                )
            for split_at in sorted(candidates):
                if split_at < 2 or len(points) - split_at < 2:
                    continue
                left = self.fit(points[order[:split_at]])
                right = self.fit(points[order[split_at:]])
                score = left["box"][0] + right["box"][0]
                candidate = (score, split_at, left, right)
                if best is None or candidate[:2] < best[:2]:
                    best = candidate
        return None if best is None else (best[2], best[3])


def primitive_from_node(node: dict, primitive_id: str, saws: list[tuple[int, int, int]]) -> dict:
    _, angle, u_min, u_max, v_min, v_max = node["box"]
    cosine = math.cos(angle)
    sine = math.sin(angle)
    center_u = (u_min + u_max) / 2
    center_v = (v_min + v_max) / 2
    center_x = center_u * cosine - center_v * sine
    center_y = center_u * sine + center_v * cosine
    rotation = math.degrees(-angle)
    width = u_max - u_min
    height = v_max - v_min
    while rotation < -90:
        rotation += 180
    while rotation >= 90:
        rotation -= 180
    if rotation <= -45:
        rotation += 90
        width, height = height, width
    elif rotation > 45:
        rotation -= 90
        width, height = height, width

    role = "static"
    if any(math.hypot(center_x - x, center_y - y) <= radius * 1.15 for x, y, radius in saws):
        role = "hazard-visual"
    return {
        "id": primitive_id,
        "primitive": "oriented-box",
        "role": role,
        "x": round((center_x - WIDTH / 2) / PLAYER_DIAMETER_PIXELS, 6),
        "y": round((HEIGHT / 2 - center_y) / PLAYER_DIAMETER_PIXELS, 6),
        "width": round(width / PLAYER_DIAMETER_PIXELS, 6),
        "height": round(height / PLAYER_DIAMETER_PIXELS, 6),
        "rotationDegrees": round(rotation, 6),
    }


def render(primitives: list[dict]) -> np.ndarray:
    rendered = np.zeros((HEIGHT, WIDTH), dtype=bool)
    for primitive in primitives:
        center_x = primitive["x"] * PLAYER_DIAMETER_PIXELS + WIDTH / 2
        center_y = HEIGHT / 2 - primitive["y"] * PLAYER_DIAMETER_PIXELS
        width = primitive["width"] * PLAYER_DIAMETER_PIXELS
        height = primitive["height"] * PLAYER_DIAMETER_PIXELS
        angle = math.radians(-primitive["rotationDegrees"])
        cosine = math.cos(angle)
        sine = math.sin(angle)
        half_aabb_x = abs(cosine) * width / 2 + abs(sine) * height / 2
        half_aabb_y = abs(sine) * width / 2 + abs(cosine) * height / 2
        x_min = max(0, int(math.floor(center_x - half_aabb_x - 1)))
        x_max = min(WIDTH, int(math.ceil(center_x + half_aabb_x + 1)))
        y_min = max(0, int(math.floor(center_y - half_aabb_y - 1)))
        y_max = min(HEIGHT, int(math.ceil(center_y + half_aabb_y + 1)))
        yy, xx = np.mgrid[y_min:y_max, x_min:x_max]
        delta_x = xx + 0.5 - center_x
        delta_y = yy + 0.5 - center_y
        local_x = delta_x * cosine + delta_y * sine
        local_y = -delta_x * sine + delta_y * cosine
        rendered[y_min:y_max, x_min:x_max] |= (
            (np.abs(local_x) <= width / 2 + 1e-9)
            & (np.abs(local_y) <= height / 2 + 1e-9)
        )
    return rendered


def decompose(mask: np.ndarray, saws: list[tuple[int, int, int]]) -> tuple[list[dict], np.ndarray, int]:
    fitter = Fitter()
    leaves: dict[int, dict] = {}
    heap: list[tuple[float, int, dict]] = []
    approximate_area = 0.0
    components = connected_components(mask)
    for points in components:
        node = fitter.fit(points)
        leaves[node["id"]] = node
        approximate_area += node["box"][0]
        heapq.heappush(heap, (-node["loss"], node["id"], node))

    target_area = int(mask.sum()) / MIN_APPROXIMATE_FILL
    while approximate_area > target_area and len(leaves) < MAX_PRIMITIVES_PER_MAP:
        _, node_id, node = heapq.heappop(heap)
        if node_id not in leaves:
            continue
        children = fitter.split(node)
        if children is None:
            break
        del leaves[node_id]
        approximate_area -= node["box"][0]
        for child in children:
            leaves[child["id"]] = child
            approximate_area += child["box"][0]
            heapq.heappush(heap, (-child["loss"], child["id"], child))

    def materialize() -> list[dict]:
        ordered = sorted(
            leaves.values(),
            key=lambda node: (
                round(float(node["points"][:, 1].mean()), 6),
                round(float(node["points"][:, 0].mean()), 6),
                node["id"],
            ),
        )
        return [
            primitive_from_node(node, f"silhouette-{index:04d}", saws)
            for index, node in enumerate(ordered, 1)
        ]

    primitives = materialize()
    rendered = render(primitives)
    return primitives, rendered, len(components)


def coarse_occupancy(mask: np.ndarray) -> np.ndarray:
    return mask.reshape(
        HEIGHT // COARSE_CELL_PIXELS,
        COARSE_CELL_PIXELS,
        WIDTH // COARSE_CELL_PIXELS,
        COARSE_CELL_PIXELS,
    ).any(axis=(1, 3))


def bounds_for(primitives: list[dict]) -> dict:
    x_values: list[float] = []
    y_values: list[float] = []
    for primitive in primitives:
        angle = math.radians(primitive["rotationDegrees"])
        half_x = (
            abs(math.cos(angle)) * primitive["width"] / 2
            + abs(math.sin(angle)) * primitive["height"] / 2
        )
        half_y = (
            abs(math.sin(angle)) * primitive["width"] / 2
            + abs(math.cos(angle)) * primitive["height"] / 2
        )
        x_values.extend((primitive["x"] - half_x, primitive["x"] + half_x))
        y_values.extend((primitive["y"] - half_y, primitive["y"] + half_y))
    return {
        "xMin": round(min(x_values), 6),
        "xMax": round(max(x_values), 6),
        "yMin": round(min(y_values), 6),
        "yMax": round(max(y_values), 6),
    }


def spawn_regions(primitives: list[dict], saws: list[tuple[int, int, int]]) -> list[dict]:
    candidates = []
    for primitive in primitives:
        if primitive["role"] != "static" or primitive["width"] < 0.25:
            continue
        angle = math.radians(primitive["rotationDegrees"])
        cosine = math.cos(angle)
        sine = math.sin(angle)
        axis_y = (-math.sin(angle), math.cos(angle))
        top_x = primitive["x"] + axis_y[0] * primitive["height"] / 2
        top_y = primitive["y"] + axis_y[1] * primitive["height"] / 2
        center_x = top_x + axis_y[0] * 0.6
        center_y = top_y + axis_y[1] * 0.6
        half_height = 0.1
        available_half_width = primitive["width"] / 2 - abs(sine) * half_height - 0.000001
        half_width = min(0.4, available_half_width / max(abs(cosine), 0.000001))
        if half_width < 0.025:
            continue
        if not (
            -18.0 <= center_x - half_width
            and center_x + half_width <= 18.0
            and -10.1 <= center_y - half_height
            and center_y + half_height <= 10.1
        ):
            continue
        pixel_x = center_x * PLAYER_DIAMETER_PIXELS + WIDTH / 2
        pixel_y = HEIGHT / 2 - center_y * PLAYER_DIAMETER_PIXELS
        region_radius_pixels = math.hypot(half_width, half_height) * PLAYER_DIAMETER_PIXELS
        if any(math.hypot(pixel_x - x, pixel_y - y) < radius + PLAYER_DIAMETER_PIXELS + region_radius_pixels for x, y, radius in saws):
            continue
        candidates.append((center_x, center_y, primitive["id"], half_width, half_height))
    if len(candidates) < 2:
        raise ValueError("Map has fewer than two source-supported spawn candidates.")
    first, second = max(
        (
            (left, right)
            for index, left in enumerate(candidates)
            for right in candidates[index + 1 :]
        ),
        key=lambda pair: (
            math.hypot(pair[1][0] - pair[0][0], pair[1][1] - pair[0][1]),
            pair[0][2],
            pair[1][2],
        ),
    )
    if math.hypot(second[0] - first[0], second[1] - first[1]) < 8:
        raise ValueError("Map cannot provide eight diameters of spawn separation.")
    left, right = sorted((first, second), key=lambda item: (item[0], item[1], item[2]))
    result = []
    for spawn_id, (x, y, support_id, half_width, half_height) in (("spawn-left", left), ("spawn-right", right)):
        result.append({
            "id": spawn_id,
            "xMin": round(x - half_width, 6),
            "xMax": round(x + half_width, 6),
            "yMin": round(y - half_height, 6),
            "yMax": round(y + half_height, 6),
            "clearanceDiameters": 1.0,
            "supportPrimitiveId": support_id,
        })
    return result


def behavior_modules(arena: int, saws: list[tuple[int, int, int]]) -> list[dict]:
    modules = []
    for index, (x, y, radius) in enumerate(saws, 1):
        modules.append({
            "id": f"saw-{index:02d}",
            "kind": "radial-saw",
            "evidenceStatus": "visible",
            "timingStatus": "unknown",
            "x": round((x - WIDTH / 2) / PLAYER_DIAMETER_PIXELS, 6),
            "y": round((HEIGHT / 2 - y) / PLAYER_DIAMETER_PIXELS, 6),
            "radius": round(radius / PLAYER_DIAMETER_PIXELS, 6),
        })
    for candidates, kind, prefix in (
        (BREAKABLE_CANDIDATES, "breakable-field", "breakable"),
        (MOVING_CANDIDATES, "moving-assembly", "moving"),
        (PHYSICS_CANDIDATES, "physics-assembly", "physics"),
    ):
        if arena in candidates:
            modules.append({
                "id": f"{prefix}-candidate-01",
                "kind": kind,
                "evidenceStatus": "visual-candidate",
                "timingStatus": "unknown",
                "bounds": {"xMin": -18.0, "xMax": 18.0, "yMin": -10.1, "yMax": 10.1},
            })
    return modules


def classify(arena: int, mask: np.ndarray, modules: list[dict]) -> tuple[str, str]:
    mirror_iou = np.logical_and(mask, np.fliplr(mask)).sum() / np.logical_or(mask, np.fliplr(mask)).sum()
    symmetry = "mirror" if mirror_iou >= 0.95 else "near-mirror" if mirror_iou >= 0.85 else "asymmetric"
    kinds = {module["kind"] for module in modules}
    if "radial-saw" in kinds:
        archetype = "hazard-course"
    elif "breakable-field" in kinds:
        archetype = "breakable-candidate"
    elif "moving-assembly" in kinds:
        archetype = "moving-candidate"
    elif "physics-assembly" in kinds:
        archetype = "physics-candidate"
    elif arena in RING_OUT_FOCUSED:
        archetype = "ring-out-islands"
    else:
        archetype = "visible-platform-layout"
    return archetype, symmetry


def main() -> None:
    arguments = parse_args()
    workbook = Path(arguments.workbook)
    output = Path(arguments.output)
    maps = []
    with zipfile.ZipFile(workbook) as archive:
        row_images = workbook_rows(archive)
        for source_row in range(2, 72):
            arena = source_row - 1
            media_name = row_images[source_row]
            media_path = f"xl/media/{media_name}"
            media_bytes = archive.read(media_path)
            mask = source_mask(archive.open(media_path))
            saws = SAWS.get(arena, [])
            primitives, rendered, source_component_count = decompose(mask, saws)
            source_coarse = coarse_occupancy(mask)
            rendered_coarse = coarse_occupancy(rendered)
            intersection = int(np.logical_and(source_coarse, rendered_coarse).sum())
            union = int(np.logical_or(source_coarse, rendered_coarse).sum())
            coarse_iou = intersection / union
            if coarse_iou < MIN_COARSE_IOU:
                raise ValueError(f"arena-{arena:03d} coarse layout IoU {coarse_iou:.6f} is below {MIN_COARSE_IOU}.")
            modules = behavior_modules(arena, saws)
            archetype, symmetry = classify(arena, mask, modules)
            collision_bounds = bounds_for(primitives)
            camera_bounds = {
                "xMin": round(min(-18.0, collision_bounds["xMin"] - 0.01), 6),
                "xMax": round(max(18.0, collision_bounds["xMax"] + 0.01), 6),
                "yMin": round(min(-10.1, collision_bounds["yMin"] - 0.01), 6),
                "yMax": round(max(10.1, collision_bounds["yMax"] + 0.01), 6),
            }
            maps.append({
                "id": f"arena-{arena:03d}",
                "sourceRow": source_row,
                "visualEvidence": {
                    "source": "community-map-sheet",
                    "previewSha256": hashlib.sha256(media_bytes).hexdigest(),
                    "previewPixels": {"width": WIDTH, "height": HEIGHT},
                    "rowAnchorMethod": "xlsx-drawing-one-cell-anchor",
                },
                "layoutEvidence": {
                    "threshold": "max-rgb-greater-than-or-equal-to-24",
                    "maskSha256": hashlib.sha256(mask.astype(np.uint8).tobytes()).hexdigest(),
                    "sourceComponentCount": source_component_count,
                    "coarseGridPixels": {"width": COARSE_CELL_PIXELS, "height": COARSE_CELL_PIXELS},
                    "sourceOccupiedCells": int(source_coarse.sum()),
                    "renderedOccupiedCells": int(rendered_coarse.sum()),
                    "renderedMaskSha256": hashlib.sha256(rendered.astype(np.uint8).tobytes()).hexdigest(),
                    "intersectionCells": intersection,
                    "unionCells": union,
                    "coarseIntersectionOverUnion": round(coarse_iou, 6),
                },
                "archetype": archetype,
                "symmetry": symmetry,
                "ringOutFocused": arena in RING_OUT_FOCUSED,
                "cameraBounds": camera_bounds,
                "collisionBounds": collision_bounds,
                "killBoundaryY": -12.0,
                "spawnRegions": spawn_regions(primitives, saws),
                "primitives": primitives,
                "behaviorModules": modules,
                "provenance": {
                    "status": "provisional",
                    "confidence": "medium",
                    "method": "Anchored source components abstracted into at most 96 oriented boxes and checked on an 8-pixel occupancy grid.",
                    "tolerance": "Coarse layout IoU >= 0.75; world scale remains provisional at +/-20 percent.",
                    "sources": ["community-map-sheet", "runtime-build-21020021"],
                },
                "unknowns": [
                    "Visible silhouette does not establish hidden or one-way colliders.",
                    "Exact spawn position, facing, and alternate spawn choices remain unmeasured.",
                    "Behavior timing and physics remain unknown unless separately measured.",
                ],
            })
            print(f"arena-{arena:03d}: {len(primitives)} boxes, coarse IoU {coarse_iou:.6f}")

    catalog = {
        "$schema": "./schema/maps.schema.json",
        "schemaVersion": 3,
        "targetBuild": "21020021",
        "targetVersion": "v1.1.2.a75ee335a",
        "catalogCount": 70,
        "units": {
            "distance": "player-diameters",
            "time": "60-hz-ticks",
            "sourcePlayerDiameterPixels": PLAYER_DIAMETER_PIXELS,
            "sourceMaskPixels": {"width": WIDTH, "height": HEIGHT},
        },
        "geometryVocabulary": [
            {"id": "oriented-box", "purpose": "Raster-verified visible silhouette box; role distinguishes collision hypotheses from hazard visuals."},
            {"id": "radial-saw", "purpose": "Visible lethal saw region with unmeasured contact and timing behavior."},
            {"id": "breakable-field", "purpose": "Visual breakable candidate pending controlled runtime confirmation."},
            {"id": "moving-assembly", "purpose": "Visual moving candidate pending controlled runtime confirmation."},
            {"id": "physics-assembly", "purpose": "Visual physics candidate pending controlled runtime confirmation."},
        ],
        "reconciliation": [
            {"source": "steam-store", "reportedCount": 70, "relationship": "lower-bound", "notes": "The official 70+ claim establishes a lower bound, not an exact active-pool count."},
            {"source": "community-map-sheet", "reportedCount": 70, "relationship": "exact-preview-index", "notes": "The public workbook has exactly one anchored preview in each sheet row 2-71."},
            {"source": "removed-vanilla-map-index", "reportedCount": 6, "relationship": "historical-removed-subset", "notes": "An independent public index lists six release-era arenas absent from all 70 workbook internal-name rows."},
            {"source": "runtime-build-21020021", "reportedCount": 0, "relationship": "observed-subset", "notes": "The current build randomizes arenas and exposes no clean-room catalog browser."},
        ],
        "representativeExamples": {
            "static": "arena-006",
            "movingCandidate": "arena-026",
            "breakableCandidate": "arena-016",
            "hazard": "arena-015",
            "asymmetric": "arena-024",
            "ringOut": "arena-006",
        },
        "maps": maps,
    }
    output.write_text(json.dumps(catalog, indent=2) + "\n", encoding="utf-8", newline="\n")


if __name__ == "__main__":
    main()
