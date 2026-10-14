"""Compare feel through the existing client replay interface; no direct state mutation."""
import argparse
import copy
import hashlib
import json
import subprocess
from pathlib import Path

def digest(value):
    return hashlib.sha256(json.dumps(value, sort_keys=True, separators=(",", ":")).encode()).hexdigest()

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--client", type=Path, required=True)
    parser.add_argument("--content", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--source-commit", required=True, help="Source identity attributed to the client executable")
    args = parser.parse_args()
    client = args.client.resolve()
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    base = json.loads(args.content.read_text(encoding="utf-8-sig"))
    # Neutral cards satisfy the ordinary opening draft without altering base combat.
    content = copy.deepcopy(base["content"])
    content["cards"] = copy.deepcopy(content["cards"][:5])
    for index, card in enumerate(content["cards"]):
        card.update(id=index + 1, title="Neutral", rules=["No combat changes."], event_rules=[])
        card["modifiers"] = {key: 0 for key in card["modifiers"]}
        card["modifiers"]["projectile_speed_factor"] = {"milli": 1000}
    arena = copy.deepcopy(content["arenas"][0])
    arena.update(name="Measured floor", spawns=[[-400.0, -257.0], [400.0, -257.0]],
                 objects=[arena["objects"][0]], chains=[], surfaces=[], legacy_bodies=[], legacy_saws=[])
    content["arenas"] = [arena]
    config = dict(base["config"], fighter_count=2, target_score=5, offer_size=5)
    (output / "base-content.json").write_text(json.dumps({"config": config, "content": content, "frames": []}, indent=2), encoding="utf-8")
    neutral = dict(move_axis=0, aim_x=1000, aim_y=0, aim_at_opponent=False,
                   jump=False, crouch=False, fire=False, block=False, flow=None)
    confirm = dict(neutral, flow={"phase_revision": 0, "action": {"Confirm": 1}})
    opening = [[confirm, confirm]] + [[neutral, neutral] for _ in range(30)]
    variants = {
        "baseline": {},
        "A": dict(run_speed=330.0, ground_control=.35, bullet_speed=1300.0, bullet_gravity_factor=.6),
        "B": dict(run_speed=300.0, ground_control=.5, air_control=.1, bullet_speed=1600.0, bullet_gravity_factor=.5),
        "C": dict(run_speed=300.0, ground_control=.5, ground_braking=.12, air_control=.1,
                  bullet_speed=1300.0, bullet_gravity_factor=.4, bullet_density=.0013846154),
    }
    results = {}
    for label, changes in variants.items():
        trial_content = copy.deepcopy(content)
        trial_content["tuning"].update(changes)
        trial_dir = output / label
        trial_dir.mkdir(exist_ok=True)
        samples = {}
        recordings = {}
        def sample(name, frames):
            recording = dict(config=config, content=trial_content, frames=opening + frames)
            recording_path = trial_dir / (name + ".recording.json")
            recording_path.write_text(json.dumps(recording), encoding="utf-8")
            process = subprocess.run([str(client), "replay", "--input", str(recording_path)],
                                     capture_output=True, text=True, check=True,
                                     creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
            state = json.loads(process.stdout)
            (trial_dir / (name + ".state.json")).write_text(json.dumps(state, indent=2), encoding="utf-8")
            assert state["flow"]["phase"] == "Combat", (label, name, state["flow"]["phase"])
            samples[name] = state
            recordings[name] = digest(recording)
            return state
        start = sample("settled", [])
        assert start["players"][0]["grounded"]
        running = dict(neutral, move_axis=1)
        for ticks in (3, 6, 12, 30, 60):
            sample("run" + str(ticks), [[running, neutral]] * ticks)
        for ticks in (3, 6, 12):
            sample("stop" + str(ticks), [[running, neutral]] * 60 + [[neutral, neutral]] * ticks)
            sample("reverse" + str(ticks), [[running, neutral]] * 60 +
                   [[dict(neutral, move_axis=-1), neutral]] * ticks)
        for ticks in (6, 12, 18, 24, 30, 36, 60):
            sample("jump" + str(ticks), [[dict(neutral, jump=True), neutral]] * ticks)
        sample("hop12", [[dict(neutral, jump=True), neutral]] * 6 + [[neutral, neutral]] * 6)
        # Aim upward from the floor so the free shot's arc stays observable.
        fire = dict(neutral, fire=True, aim_y=300)
        for ticks in (1, 4, 8, 12, 16, 20):
            sample("shot" + str(ticks), [[fire, neutral]] + [[neutral, neutral]] * (ticks - 1))
        sample("block1", [[dict(neutral, block=True), neutral]])
        # Repeat an unchanged recording exactly; snapshot identity is the determinism assertion.
        reference = copy.deepcopy(samples["run60"])
        assert sample("run60", [[running, neutral]] * 60) == reference
        p0 = start["players"][0]
        run60 = samples["run60"]["players"][0]
        jump_states = [samples["jump" + str(t)]["players"][0] for t in (6,12,18,24,30,36,60)]
        projectile = samples["shot1"]["projectiles"]
        assert projectile and samples["shot1"]["metrics"]["shotsFired"] == 1
        assert samples["block1"]["metrics"]["blockActivations"] == 1
        # Compare idle riding through an actual moving Solid object's public data.
        original_arena = copy.deepcopy(trial_content["arenas"][0])
        moving = copy.deepcopy(original_arena["objects"][0])
        moving.update(id=11, position=[-200.0, -150.0], shape={"Rectangle": {"size": [130.0, 16.0]}},
                      motion={"path": [[0.0, 0.0], [200.0, 0.0]], "period_seconds": 4.0, "angular_velocity": 0.0})
        trial_content["arenas"][0]["objects"].append(moving)
        trial_content["arenas"][0]["spawns"][0] = [-200.0, -120.0]
        ride0 = sample("ride0", [])
        ride60 = sample("ride60", [[neutral, neutral]] * 60)
        platform = lambda s: next(o for o in s["arenaObjects"]["objects"] if o["id"] == 11)["position"][0]
        relative0 = ride0["players"][0]["xMilli"] / 1000 - platform(ride0)
        relative60 = ride60["players"][0]["xMilli"] / 1000 - platform(ride60)
        riding_drift = relative60 - relative0
        trial_content["arenas"][0] = copy.deepcopy(original_arena)
        contacts = {}
        for distance in (200, 360, 480, 640):
            trial_content["arenas"][0]["spawns"] = [[-400.0, -257.0], [-400.0 + distance, -257.0]]
            contact = None
            for ticks in range(1, 41):
                state = sample(f"direct{distance}_{ticks}", [[dict(neutral, fire=True), neutral]] +
                               [[neutral, neutral]] * (ticks - 1))
                if state["metrics"]["hits"]:
                    contact = ticks
                    break
                if not state["projectiles"]:
                    break
            contacts[str(distance)] = {"hit_tick": contact, "flight_seconds": None if contact is None else contact / 60}
        trial_content["arenas"][0] = original_arena
        speed = (projectile[0]["velocityXMilliPerSecond"]**2 +
                 projectile[0]["velocityYMilliPerSecond"]**2)**.5 / 1000
        results[label] = {
            "tuning_changes": changes,
            "ground_speed_px_s": run60["velocityXMilliPerSecond"] / 1000,
            "one_second_run_px": (run60["xMilli"] - p0["xMilli"]) / 1000,
            "run6_speed_px_s": samples["run6"]["players"][0]["velocityXMilliPerSecond"] / 1000,
            "stop3_speed_px_s": samples["stop3"]["players"][0]["velocityXMilliPerSecond"] / 1000,
            "stop12_displacement_px": (samples["stop12"]["players"][0]["xMilli"] - run60["xMilli"]) / 1000,
            "reverse6_speed_px_s": samples["reverse6"]["players"][0]["velocityXMilliPerSecond"] / 1000,
            "sampled_jump_apex_px": max(p["yMilli"] for p in jump_states) / 1000 - p0["yMilli"] / 1000,
            "hop12_height_px": samples["hop12"]["players"][0]["yMilli"]/1000 - p0["yMilli"]/1000,
            "shot_first_tick_speed_px_s": round(speed, 3),
            "nominal_400px_flight_seconds": round(400 / trial_content["tuning"]["bullet_speed"], 4),
            "free_shot_samples": {str(t): samples["shot"+str(t)]["projectiles"] for t in (1,4,8,12,16,20)},
            "recording_hashes": recordings,
            "state_hashes": {k: digest(v) for k,v in samples.items()},
            "repeat_run60_equal": True,
            "idle_platform_relative_drift_px_per_second": round(riding_drift, 3),
            "direct_aim_contacts": contacts,
        }
    report = {"source_commit": args.source_commit,
              "client_sha256": hashlib.sha256(client.read_bytes()).hexdigest(),
              "content_sha256": hashlib.sha256(args.content.read_bytes()).hexdigest(),
              "trials": results, "limits": [
                  "Synthetic neutral opening cards and flat floor isolate base tuning.",
                  "Nominal flight time is distance divided by configured speed, not measured contact time.",
                  "Jump apex is sampled every six ticks; it is not the exact apex.",
                  "No human preference or audiovisual feel is inferred from these metrics."]}
    (output / "report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps({k: {key:value for key,value in v.items() if key not in ("free_shot_samples","recording_hashes","state_hashes")}
                      for k,v in results.items()}, indent=2))

if __name__ == "__main__":
    main()

