use bevy_ecs::prelude::*;
use serde::{Deserialize, Serialize};
use sha2::{Digest, Sha256};

pub const TICKS_PER_SECOND: u32 = 60;
pub const MAX_INSPECTED_PROJECTILES: usize = 64;
const GROUND_Y: i32 = 1_000;
const LEFT_WALL: i32 = -15_000;
const RIGHT_WALL: i32 = 15_000;
const PLAYER_SPEED: i32 = 150;
const GRAVITY: i32 = -35;
const JUMP_SPEED: i32 = 700;
const PROJECTILE_SPEED: i32 = 850;
const PROJECTILE_LIFETIME: u16 = 120;
const FIRE_COOLDOWN: u16 = 18;
const BLOCK_DURATION: u16 = 12;
const HIT_RADIUS_SQUARED: i64 = 1_300_i64 * 1_300_i64;

#[derive(Clone, Copy, Debug, Default, Deserialize, PartialEq, Eq, Serialize)]
pub struct PlayerInput {
    pub move_axis: i8,
    pub aim_x: i16,
    pub aim_y: i16,
    pub jump: bool,
    pub fire: bool,
    pub block: bool,
}

impl PlayerInput {
    pub fn validated(self) -> Self {
        Self {
            move_axis: self.move_axis.clamp(-1, 1),
            aim_x: self.aim_x.clamp(-1_000, 1_000),
            aim_y: self.aim_y.clamp(-1_000, 1_000),
            ..self
        }
    }
}

#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
pub struct PlayerSnapshot {
    pub id: u8,
    pub x_milli: i32,
    pub y_milli: i32,
    pub velocity_x_milli_per_tick: i32,
    pub velocity_y_milli_per_tick: i32,
    pub health: u16,
    pub fire_cooldown_ticks: u16,
    pub block_ticks: u16,
}

#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
pub struct ProjectileSnapshot {
    pub id: u32,
    pub owner: u8,
    pub x_milli: i32,
    pub y_milli: i32,
    pub velocity_x_milli_per_tick: i32,
    pub velocity_y_milli_per_tick: i32,
    pub lifetime_ticks: u16,
}

#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
pub struct MatchSnapshot {
    pub protocol: u16,
    pub seed: u64,
    pub tick: u32,
    pub players: Vec<PlayerSnapshot>,
    pub projectiles: Vec<ProjectileSnapshot>,
}

#[derive(Component, Clone, Copy)]
struct PlayerId(u8);

#[derive(Component, Clone, Copy)]
struct PlayerBody {
    x: i32,
    y: i32,
    velocity_x: i32,
    velocity_y: i32,
}

#[derive(Component, Clone, Copy)]
struct CombatState {
    health: u16,
    fire_cooldown: u16,
    block_ticks: u16,
}

#[derive(Component, Clone, Copy)]
struct Projectile {
    id: u32,
    owner: u8,
    x: i32,
    y: i32,
    velocity_x: i32,
    velocity_y: i32,
    lifetime: u16,
}

#[derive(Resource)]
struct MatchClock {
    seed: u64,
    tick: u32,
    next_projectile_id: u32,
}

#[derive(Resource)]
struct TickInputs([PlayerInput; 2]);

#[derive(Resource, Clone, Copy)]
struct PlayerEntities([Entity; 2]);

pub struct AuthoritativeMatch {
    world: World,
    fixed_schedule: Schedule,
}

impl AuthoritativeMatch {
    pub fn new(seed: u64) -> Self {
        let mut world = World::new();
        let player_zero = world
            .spawn((
                PlayerId(0),
                PlayerBody {
                    x: -6_000,
                    y: GROUND_Y,
                    velocity_x: 0,
                    velocity_y: 0,
                },
                CombatState {
                    health: 100,
                    fire_cooldown: 0,
                    block_ticks: 0,
                },
            ))
            .id();
        let player_one = world
            .spawn((
                PlayerId(1),
                PlayerBody {
                    x: 6_000,
                    y: GROUND_Y,
                    velocity_x: 0,
                    velocity_y: 0,
                },
                CombatState {
                    health: 100,
                    fire_cooldown: 0,
                    block_ticks: 0,
                },
            ))
            .id();
        world.insert_resource(MatchClock {
            seed,
            tick: 0,
            next_projectile_id: 1,
        });
        world.insert_resource(TickInputs([PlayerInput::default(); 2]));
        world.insert_resource(PlayerEntities([player_zero, player_one]));

        let mut fixed_schedule = Schedule::default();
        fixed_schedule.add_systems(fixed_tick);
        Self {
            world,
            fixed_schedule,
        }
    }

    pub fn step(&mut self, inputs: [PlayerInput; 2]) {
        self.world.resource_mut::<TickInputs>().0 = inputs.map(PlayerInput::validated);
        self.fixed_schedule.run(&mut self.world);
    }

    pub fn snapshot(&mut self) -> MatchSnapshot {
        let clock = self.world.resource::<MatchClock>();
        let seed = clock.seed;
        let tick = clock.tick;
        let mut players = self
            .world
            .query::<(&PlayerId, &PlayerBody, &CombatState)>()
            .iter(&self.world)
            .map(|(id, body, combat)| PlayerSnapshot {
                id: id.0,
                x_milli: body.x,
                y_milli: body.y,
                velocity_x_milli_per_tick: body.velocity_x,
                velocity_y_milli_per_tick: body.velocity_y,
                health: combat.health,
                fire_cooldown_ticks: combat.fire_cooldown,
                block_ticks: combat.block_ticks,
            })
            .collect::<Vec<_>>();
        players.sort_by_key(|player| player.id);
        let mut projectiles = self
            .world
            .query::<&Projectile>()
            .iter(&self.world)
            .map(|projectile| ProjectileSnapshot {
                id: projectile.id,
                owner: projectile.owner,
                x_milli: projectile.x,
                y_milli: projectile.y,
                velocity_x_milli_per_tick: projectile.velocity_x,
                velocity_y_milli_per_tick: projectile.velocity_y,
                lifetime_ticks: projectile.lifetime,
            })
            .collect::<Vec<_>>();
        projectiles.sort_by_key(|projectile| projectile.id);
        projectiles.truncate(MAX_INSPECTED_PROJECTILES);
        MatchSnapshot {
            protocol: 1,
            seed,
            tick,
            players,
            projectiles,
        }
    }

    pub fn state_hash(&mut self) -> String {
        hash_snapshot(&self.snapshot())
    }
}

pub fn hash_snapshot(snapshot: &MatchSnapshot) -> String {
    let bytes = serde_json::to_vec(snapshot).expect("snapshot serialization cannot fail");
    format!("{:x}", Sha256::digest(bytes))
}

pub fn scripted_inputs(seed: u64, ticks: u32) -> [Vec<PlayerInput>; 2] {
    let mut scripts = [
        Vec::with_capacity(ticks as usize),
        Vec::with_capacity(ticks as usize),
    ];
    for tick in 0..ticks {
        let phase = ((tick as u64 + seed % 29) / 45) % 2;
        scripts[0].push(PlayerInput {
            move_axis: if phase == 0 { 1 } else { -1 },
            aim_x: 1_000,
            aim_y: if tick % 90 < 15 { 180 } else { 0 },
            jump: tick % 91 == 12,
            fire: tick % 23 == 3,
            block: tick % 127 == 29,
        });
        scripts[1].push(PlayerInput {
            move_axis: if phase == 0 { -1 } else { 1 },
            aim_x: -1_000,
            aim_y: if tick % 80 < 12 { 120 } else { 0 },
            jump: tick % 83 == 19,
            fire: tick % 29 == 7,
            block: tick % 113 == 41,
        });
    }
    scripts
}

pub fn run_scripted_match(seed: u64, ticks: u32) -> (MatchSnapshot, String) {
    let scripts = scripted_inputs(seed, ticks);
    let mut simulation = AuthoritativeMatch::new(seed);
    for (player_zero, player_one) in scripts[0].iter().copied().zip(scripts[1].iter().copied()) {
        simulation.step([player_zero, player_one]);
    }
    let snapshot = simulation.snapshot();
    let state_hash = hash_snapshot(&snapshot);
    (snapshot, state_hash)
}

fn fixed_tick(world: &mut World) {
    let inputs = world.resource::<TickInputs>().0;
    let entities = world.resource::<PlayerEntities>().0;
    let projectile_count = world.query::<&Projectile>().iter(world).count();
    let mut spawns = Vec::with_capacity(2);

    for (index, entity) in entities.into_iter().enumerate() {
        let input = inputs[index];
        let (x, y, can_fire) = {
            let mut player = world.entity_mut(entity);
            let mut body = player.get_mut::<PlayerBody>().expect("player body");
            body.velocity_x = i32::from(input.move_axis) * PLAYER_SPEED;
            if input.jump && body.y == GROUND_Y {
                body.velocity_y = JUMP_SPEED;
            }
            (body.x, body.y, input.fire)
        };
        let mut player = world.entity_mut(entity);
        let mut combat = player.get_mut::<CombatState>().expect("combat state");
        combat.fire_cooldown = combat.fire_cooldown.saturating_sub(1);
        combat.block_ticks = combat.block_ticks.saturating_sub(1);
        if input.block {
            combat.block_ticks = BLOCK_DURATION;
        }
        if can_fire
            && combat.fire_cooldown == 0
            && projectile_count + spawns.len() < MAX_INSPECTED_PROJECTILES
        {
            combat.fire_cooldown = FIRE_COOLDOWN;
            let aim_x = if input.aim_x == 0 {
                if index == 0 { 1_000 } else { -1_000 }
            } else {
                input.aim_x
            };
            spawns.push((index as u8, x, y, aim_x, input.aim_y));
        }
    }

    for entity in entities {
        let mut player = world.entity_mut(entity);
        let mut body = player.get_mut::<PlayerBody>().expect("player body");
        body.velocity_y += GRAVITY;
        body.x = (body.x + body.velocity_x).clamp(LEFT_WALL, RIGHT_WALL);
        body.y += body.velocity_y;
        if body.y <= GROUND_Y {
            body.y = GROUND_Y;
            body.velocity_y = 0;
        }
    }

    for (owner, x, y, aim_x, aim_y) in spawns {
        let divisor = i32::from(aim_x.unsigned_abs().max(aim_y.unsigned_abs()).max(1));
        let projectile_id = {
            let mut clock = world.resource_mut::<MatchClock>();
            let id = clock.next_projectile_id;
            clock.next_projectile_id += 1;
            id
        };
        world.spawn(Projectile {
            id: projectile_id,
            owner,
            x,
            y,
            velocity_x: i32::from(aim_x) * PROJECTILE_SPEED / divisor,
            velocity_y: i32::from(aim_y) * PROJECTILE_SPEED / divisor,
            lifetime: PROJECTILE_LIFETIME,
        });
    }

    let projectile_entities = world
        .query::<(Entity, &Projectile)>()
        .iter(world)
        .map(|(entity, _)| entity)
        .collect::<Vec<_>>();
    for entity in projectile_entities {
        let expired = {
            let mut projectile_entity = world.entity_mut(entity);
            let mut projectile = projectile_entity
                .get_mut::<Projectile>()
                .expect("projectile");
            projectile.x += projectile.velocity_x;
            projectile.y += projectile.velocity_y;
            projectile.lifetime = projectile.lifetime.saturating_sub(1);
            projectile.lifetime == 0
                || projectile.x < LEFT_WALL - 2_000
                || projectile.x > RIGHT_WALL + 2_000
                || projectile.y < -2_000
                || projectile.y > 22_000
        };
        if expired {
            world.despawn(entity);
        }
    }

    let projectiles = world
        .query::<(Entity, &Projectile)>()
        .iter(world)
        .map(|(entity, projectile)| (entity, *projectile))
        .collect::<Vec<_>>();
    for (projectile_entity, projectile) in projectiles {
        let target_index = usize::from(1 - projectile.owner);
        let target_entity = entities[target_index];
        let (target_x, target_y, blocking) = {
            let target = world.entity(target_entity);
            let body = target.get::<PlayerBody>().expect("player body");
            let combat = target.get::<CombatState>().expect("combat state");
            (body.x, body.y, combat.block_ticks > 0)
        };
        let dx = i64::from(projectile.x - target_x);
        let dy = i64::from(projectile.y - target_y);
        if dx * dx + dy * dy > HIT_RADIUS_SQUARED {
            continue;
        }
        if blocking {
            let mut bullet = world.entity_mut(projectile_entity);
            let mut reflected = bullet.get_mut::<Projectile>().expect("projectile");
            reflected.owner = target_index as u8;
            reflected.velocity_x = -reflected.velocity_x;
            reflected.velocity_y = -reflected.velocity_y;
            continue;
        }
        {
            let mut target = world.entity_mut(target_entity);
            let mut combat = target.get_mut::<CombatState>().expect("combat state");
            combat.health = combat.health.saturating_sub(25);
        }
        world.despawn(projectile_entity);
    }

    world.resource_mut::<MatchClock>().tick += 1;
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn scripted_match_is_repeatable_and_bounded() {
        let first = run_scripted_match(38, 240);
        let second = run_scripted_match(38, 240);
        assert_eq!(first, second);
        assert_eq!(first.0.tick, 240);
        assert_eq!(first.0.players.len(), 2);
        assert!(first.0.projectiles.len() <= MAX_INSPECTED_PROJECTILES);
    }

    #[test]
    fn input_is_clamped_at_the_authoritative_boundary() {
        let mut simulation = AuthoritativeMatch::new(1);
        simulation.step([
            PlayerInput {
                move_axis: 100,
                aim_x: i16::MAX,
                aim_y: i16::MIN,
                ..PlayerInput::default()
            },
            PlayerInput::default(),
        ]);
        let snapshot = simulation.snapshot();
        assert_eq!(snapshot.players[0].velocity_x_milli_per_tick, PLAYER_SPEED);
    }

    #[test]
    fn snapshot_json_is_small_enough_for_machine_inspection() {
        let (snapshot, _) = run_scripted_match(38, 600);
        let json = serde_json::to_vec(&snapshot).unwrap();
        assert!(json.len() < 16_384, "{} bytes", json.len());
    }
}
