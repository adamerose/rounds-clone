use rounds_sim::{
    AuthoritativeMatch, FlowPhase, ItemId, MatchSnapshot, PlayerInput, PriorBadge,
    RADIAL_HALF_BLUE_TICK, RADIAL_RESULT_ONSET_TICK, ReplayProfile, RoundPhase, TIMBER_IMPACT_TICK,
    arena_digest, combat_digest, dynamic_body_digest, flow_digest, hash_snapshot, loadout_digest,
    round_digest, saw_digest,
};
use serde::{Deserialize, Serialize};
use std::io;
use std::net::{SocketAddr, ToSocketAddrs, UdpSocket};
use std::time::Duration;

pub const NETWORK_PROTOCOL: u16 = 4;
pub const MAX_NETWORK_TICKS: u32 = 3_000;
const MAX_DATAGRAM_BYTES: usize = 65_507;

#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(tag = "kind", rename_all = "camelCase")]
enum ClientPacket {
    Hello {
        protocol: u16,
        client_id: u8,
        seed: u64,
        profile: ReplayProfile,
    },
    Input {
        protocol: u16,
        client_id: u8,
        sequence: u32,
        input: PlayerInput,
    },
}

#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(tag = "kind", rename_all = "camelCase")]
enum AuthorityPacket {
    Welcome {
        protocol: u16,
        client_id: u8,
    },
    Snapshot {
        protocol: u16,
        sequence: u32,
        state_hash: String,
        state: Box<MatchSnapshot>,
    },
}

#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ServerReport {
    pub protocol: u16,
    pub clients_handshaken: u8,
    pub inputs_received: u32,
    pub progressive_snapshots: u32,
    pub first_snapshot_tick: u32,
    pub last_snapshot_tick: u32,
    pub state_hash: String,
    pub dynamic_body_digest: String,
    pub arena_digest: String,
    pub saw_digest: String,
    pub combat_digest: String,
    pub round_digest: Option<String>,
    pub flow_digest: Option<String>,
    pub loadout_digest: Option<String>,
    pub state: MatchSnapshot,
}

#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ClientSessionReport {
    pub protocol: u16,
    pub client_id: u8,
    pub handshake_complete: bool,
    pub inputs_sent: u32,
    pub first_input_sequence: u32,
    pub last_input_sequence: u32,
    pub snapshots_received: u32,
    pub first_snapshot_tick: u32,
    pub last_snapshot_tick: u32,
    pub observed_pre_explosion_constraints: bool,
    pub observed_post_explosion_release: bool,
    pub observed_flow_phases: Vec<FlowPhase>,
    pub observed_source_terminal_state: bool,
    pub observed_rematch_reset: bool,
    pub observed_blue_fan_by_tick_960: bool,
    pub observed_radial_saw_motion: bool,
    pub observed_radial_damage: bool,
    pub observed_radial_result_onset: bool,
    pub observed_radial_half_blue: bool,
    pub final_report: ServerReport,
}

pub struct BoundServer {
    socket: UdpSocket,
}

impl BoundServer {
    pub fn bind(address: impl ToSocketAddrs) -> io::Result<Self> {
        let socket = UdpSocket::bind(address)?;
        socket.set_read_timeout(Some(Duration::from_secs(10)))?;
        socket.set_write_timeout(Some(Duration::from_secs(10)))?;
        Ok(Self { socket })
    }

    pub fn local_addr(&self) -> io::Result<SocketAddr> {
        self.socket.local_addr()
    }

    pub fn run(
        self,
        expected_seed: u64,
        expected_ticks: u32,
        expected_profile: ReplayProfile,
    ) -> Result<ServerReport, String> {
        validate_tick_count(expected_ticks)?;
        let mut clients: [Option<SocketAddr>; 2] = [None, None];
        while clients.iter().any(Option::is_none) {
            let (packet, sender) = receive_client(&self.socket)?;
            let ClientPacket::Hello {
                protocol,
                client_id,
                seed,
                profile,
            } = packet
            else {
                return Err("input arrived before both client handshakes".to_owned());
            };
            validate_protocol(protocol)?;
            if client_id > 1 {
                return Err(format!("client id {client_id} is outside 0..=1"));
            }
            if seed != expected_seed {
                return Err(format!("client {client_id} used the wrong seed"));
            }
            if profile != expected_profile {
                return Err(format!("client {client_id} used the wrong replay profile"));
            }
            let slot = &mut clients[usize::from(client_id)];
            if slot.is_some() {
                return Err(format!("duplicate handshake for client {client_id}"));
            }
            *slot = Some(sender);
        }
        let clients = clients.map(Option::unwrap);
        for (client_id, address) in clients.into_iter().enumerate() {
            send_authority(
                &self.socket,
                address,
                &AuthorityPacket::Welcome {
                    protocol: NETWORK_PROTOCOL,
                    client_id: client_id as u8,
                },
            )?;
        }
        let mut simulation = AuthoritativeMatch::new_with_profile(expected_seed, expected_profile);
        let mut final_state = None;
        for sequence in 0..expected_ticks {
            let mut inputs = [None, None];
            while inputs.iter().any(Option::is_none) {
                let (packet, sender) = receive_client(&self.socket)?;
                let ClientPacket::Input {
                    protocol,
                    client_id,
                    sequence: received_sequence,
                    input,
                } = packet
                else {
                    return Err("duplicate handshake after the match started".to_owned());
                };
                validate_protocol(protocol)?;
                if client_id > 1 || clients[usize::from(client_id)] != sender {
                    return Err("input sender did not match its handshake".to_owned());
                }
                if received_sequence != sequence {
                    return Err(format!(
                        "client {client_id} sent sequence {received_sequence}; expected {sequence}"
                    ));
                }
                let slot = &mut inputs[usize::from(client_id)];
                if slot.is_some() {
                    return Err(format!(
                        "duplicate input sequence {sequence} from client {client_id}"
                    ));
                }
                *slot = Some(input.validated());
            }
            simulation.step(inputs.map(Option::unwrap));
            let state = simulation.snapshot();
            let state_hash = hash_snapshot(&state);
            let packet = AuthorityPacket::Snapshot {
                protocol: NETWORK_PROTOCOL,
                sequence,
                state_hash: state_hash.clone(),
                state: Box::new(state.clone()),
            };
            for address in clients {
                send_authority(&self.socket, address, &packet)?;
            }
            final_state = Some((state, state_hash));
        }
        let (state, state_hash) = final_state.expect("validated non-zero tick count");
        Ok(ServerReport {
            protocol: NETWORK_PROTOCOL,
            clients_handshaken: 2,
            inputs_received: expected_ticks * 2,
            progressive_snapshots: expected_ticks,
            first_snapshot_tick: 1,
            last_snapshot_tick: state.tick,
            state_hash,
            dynamic_body_digest: dynamic_body_digest(&state),
            arena_digest: arena_digest(&state),
            saw_digest: saw_digest(&state),
            combat_digest: combat_digest(&state),
            round_digest: round_digest(&state),
            flow_digest: state.flow.as_ref().map(flow_digest),
            loadout_digest: state.flow.as_ref().map(loadout_digest),
            state,
        })
    }
}

pub fn send_inputs(
    address: impl ToSocketAddrs,
    client_id: u8,
    seed: u64,
    profile: ReplayProfile,
    inputs: &[PlayerInput],
) -> Result<ClientSessionReport, String> {
    if client_id > 1 {
        return Err(format!("client id {client_id} is outside 0..=1"));
    }
    validate_tick_count(inputs.len() as u32)?;
    let server = address
        .to_socket_addrs()
        .map_err(|error| format!("resolve authority: {error}"))?
        .next()
        .ok_or("authority address did not resolve")?;
    let socket =
        UdpSocket::bind("127.0.0.1:0").map_err(|error| format!("bind client socket: {error}"))?;
    socket
        .set_read_timeout(Some(Duration::from_secs(10)))
        .map_err(|error| format!("set client timeout: {error}"))?;
    send_client(
        &socket,
        server,
        &ClientPacket::Hello {
            protocol: NETWORK_PROTOCOL,
            client_id,
            seed,
            profile,
        },
    )?;
    match receive_authority(&socket, server)? {
        AuthorityPacket::Welcome {
            protocol,
            client_id: welcomed,
        } if protocol == NETWORK_PROTOCOL && welcomed == client_id => {}
        _ => return Err("authority returned an invalid handshake".to_owned()),
    }

    let mut last = None;
    let mut observed_pre_explosion_constraints = false;
    let mut observed_post_explosion_release = false;
    let mut observed_flow_phases = Vec::new();
    let mut observed_source_terminal_state = false;
    let mut observed_rematch_reset = false;
    let mut observed_blue_fan_by_tick_960 = false;
    let mut first_radial_angles = None;
    let mut observed_radial_saw_motion = false;
    let mut observed_radial_damage = false;
    let mut observed_radial_result_onset = false;
    let mut observed_radial_half_blue = false;
    for (sequence, input) in inputs.iter().copied().enumerate() {
        let sequence = sequence as u32;
        send_client(
            &socket,
            server,
            &ClientPacket::Input {
                protocol: NETWORK_PROTOCOL,
                client_id,
                sequence,
                input,
            },
        )?;
        match receive_authority(&socket, server)? {
            AuthorityPacket::Snapshot {
                protocol,
                sequence: received_sequence,
                state_hash,
                state,
            } => {
                validate_protocol(protocol)?;
                if received_sequence != sequence || state.tick != sequence + 1 {
                    return Err(format!(
                        "snapshot sequence {received_sequence} at tick {}; expected {sequence}",
                        state.tick
                    ));
                }
                if state_hash != hash_snapshot(&state) {
                    return Err("authority snapshot hash did not match its payload".to_owned());
                }
                if profile == ReplayProfile::TimberCollapseReplay {
                    if state.tick < TIMBER_IMPACT_TICK
                        && !state.constraints.is_empty()
                        && state.constraints.iter().all(|constraint| constraint.active)
                    {
                        observed_pre_explosion_constraints = true;
                    }
                    if state.tick >= TIMBER_IMPACT_TICK
                        && !state.explosions.is_empty()
                        && state
                            .constraints
                            .iter()
                            .any(|constraint| !constraint.active)
                    {
                        observed_post_explosion_release = true;
                    }
                }
                if let Some(flow) = &state.flow
                    && observed_flow_phases.last() != Some(&flow.phase)
                {
                    observed_flow_phases.push(flow.phase);
                }
                if let Some(flow) = &state.flow {
                    observed_source_terminal_state |= flow.scores == [4, 5]
                        && flow.winner == Some(1)
                        && flow.eliminated == Some(0)
                        && flow.fighter_alive == [false, true]
                        && flow.prior_badges
                            == [
                                vec![
                                    PriorBadge::Po,
                                    PriorBadge::De,
                                    PriorBadge::Th,
                                    PriorBadge::Qu,
                                    PriorBadge::Bu,
                                ],
                                vec![
                                    PriorBadge::Bu,
                                    PriorBadge::Ca,
                                    PriorBadge::Co,
                                    PriorBadge::Co,
                                    PriorBadge::Fa,
                                ],
                            ]
                        && state.winner == Some(1)
                        && !state.players[0].alive
                        && state.players[1].alive;
                    observed_rematch_reset |= flow.scores == [0, 0]
                        && flow.winner.is_none()
                        && flow.eliminated.is_none()
                        && flow.fighter_alive == [true, true]
                        && flow.prior_badges.iter().all(Vec::is_empty)
                        && state.winner.is_none()
                        && state.players.iter().all(|player| player.alive);
                    observed_blue_fan_by_tick_960 |= state.tick == 960
                        && flow.phase == FlowPhase::Draft
                        && flow.active_player == Some(1)
                        && flow.offers[1].len() == 5
                        && flow.hovered[1] == Some(ItemId::Dazzle)
                        && flow.selected[0] == Some(ItemId::Dazzle);
                }
                if profile == ReplayProfile::RadialSawHalfBlueReplay {
                    let angles = state
                        .saws
                        .iter()
                        .map(|saw| saw.angle_milliradians)
                        .collect::<Vec<_>>();
                    if first_radial_angles.is_none() {
                        first_radial_angles = Some(angles.clone());
                    }
                    observed_radial_saw_motion |= first_radial_angles
                        .as_ref()
                        .is_some_and(|initial| initial != &angles)
                        && state.saws.iter().map(|saw| saw.id).collect::<Vec<_>>() == [200, 201]
                        && state
                            .saws
                            .iter()
                            .all(|saw| saw.angular_velocity_milliradians_per_second == 7_430);
                    observed_radial_damage |= state.metrics.hits > 0
                        && state.players.iter().any(|player| player.health < 100);
                    observed_radial_result_onset |= state.tick == RADIAL_RESULT_ONSET_TICK
                        && state.round.as_ref().is_some_and(|round| {
                            round.phase == RoundPhase::ResultTransition
                                && round.phase_tick == 0
                                && round.scores == [1, 1]
                                && round.winner == Some(1)
                                && round.eliminated == Some(0)
                        });
                    observed_radial_half_blue |= state.tick == RADIAL_HALF_BLUE_TICK
                        && state.round.as_ref().is_some_and(|round| {
                            round.phase == RoundPhase::HalfBlue
                                && round.scores == [1, 1]
                                && round.winner == Some(1)
                        });
                }
                last = Some((*state, state_hash));
            }
            AuthorityPacket::Welcome { .. } => {
                return Err("authority repeated its handshake".to_owned());
            }
        }
    }
    let (state, state_hash) = last.expect("validated non-empty input sequence");
    let ticks = inputs.len() as u32;
    Ok(ClientSessionReport {
        protocol: NETWORK_PROTOCOL,
        client_id,
        handshake_complete: true,
        inputs_sent: ticks,
        first_input_sequence: 0,
        last_input_sequence: ticks - 1,
        snapshots_received: ticks,
        first_snapshot_tick: 1,
        last_snapshot_tick: ticks,
        observed_pre_explosion_constraints,
        observed_post_explosion_release,
        observed_flow_phases,
        observed_source_terminal_state,
        observed_rematch_reset,
        observed_blue_fan_by_tick_960,
        observed_radial_saw_motion,
        observed_radial_damage,
        observed_radial_result_onset,
        observed_radial_half_blue,
        final_report: ServerReport {
            protocol: NETWORK_PROTOCOL,
            clients_handshaken: 2,
            inputs_received: ticks * 2,
            progressive_snapshots: ticks,
            first_snapshot_tick: 1,
            last_snapshot_tick: ticks,
            state_hash,
            dynamic_body_digest: dynamic_body_digest(&state),
            arena_digest: arena_digest(&state),
            saw_digest: saw_digest(&state),
            combat_digest: combat_digest(&state),
            round_digest: round_digest(&state),
            flow_digest: state.flow.as_ref().map(flow_digest),
            loadout_digest: state.flow.as_ref().map(loadout_digest),
            state,
        },
    })
}

fn validate_tick_count(ticks: u32) -> Result<(), String> {
    if ticks == 0 || ticks > MAX_NETWORK_TICKS {
        return Err(format!(
            "network tick count must be between 1 and {MAX_NETWORK_TICKS}"
        ));
    }
    Ok(())
}

fn validate_protocol(protocol: u16) -> Result<(), String> {
    if protocol != NETWORK_PROTOCOL {
        return Err(format!("unsupported network protocol {protocol}"));
    }
    Ok(())
}

fn send_client(
    socket: &UdpSocket,
    address: SocketAddr,
    packet: &ClientPacket,
) -> Result<(), String> {
    send_bytes(socket, address, packet, "client packet")
}

fn send_authority(
    socket: &UdpSocket,
    address: SocketAddr,
    packet: &AuthorityPacket,
) -> Result<(), String> {
    send_bytes(socket, address, packet, "authority packet")
}

fn send_bytes<T: Serialize>(
    socket: &UdpSocket,
    address: SocketAddr,
    value: &T,
    label: &str,
) -> Result<(), String> {
    let bytes = serde_json::to_vec(value).map_err(|error| error.to_string())?;
    if bytes.len() > MAX_DATAGRAM_BYTES {
        return Err(format!("{label} is too large: {} bytes", bytes.len()));
    }
    socket
        .send_to(&bytes, address)
        .map_err(|error| format!("send {label}: {error}"))?;
    Ok(())
}

fn receive_client(socket: &UdpSocket) -> Result<(ClientPacket, SocketAddr), String> {
    let (bytes, sender) = receive_bytes(socket, "client packet")?;
    let packet =
        serde_json::from_slice(&bytes).map_err(|error| format!("decode client packet: {error}"))?;
    Ok((packet, sender))
}

fn receive_authority(socket: &UdpSocket, server: SocketAddr) -> Result<AuthorityPacket, String> {
    let (bytes, sender) = receive_bytes(socket, "authority packet")?;
    if sender != server {
        return Err("received authority packet from an unexpected sender".to_owned());
    }
    serde_json::from_slice(&bytes).map_err(|error| format!("decode authority packet: {error}"))
}

fn receive_bytes(socket: &UdpSocket, label: &str) -> Result<(Vec<u8>, SocketAddr), String> {
    let mut buffer = vec![0_u8; MAX_DATAGRAM_BYTES];
    let (length, sender) = socket
        .recv_from(&mut buffer)
        .map_err(|error| format!("receive {label}: {error}"))?;
    buffer.truncate(length);
    Ok((buffer, sender))
}

#[cfg(test)]
mod tests {
    use super::*;
    use rounds_sim::{REPLAY_TICKS, run_scripted_match, scripted_inputs, scripted_inputs_for};
    use std::thread;

    #[test]
    fn two_udp_clients_stream_monotonic_inputs_and_progressive_snapshots() {
        let seed = 38;
        let ticks = REPLAY_TICKS;
        let scripts = scripted_inputs(seed, ticks);
        let server = BoundServer::bind("127.0.0.1:0").unwrap();
        let address = server.local_addr().unwrap();
        let profile = ReplayProfile::TimberCollapseReplay;
        let server_thread = thread::spawn(move || server.run(seed, ticks, profile).unwrap());
        let clients = scripts
            .into_iter()
            .enumerate()
            .map(|(client_id, inputs)| {
                thread::spawn(move || {
                    send_inputs(address, client_id as u8, seed, profile, &inputs).unwrap()
                })
            })
            .collect::<Vec<_>>();
        let reports = clients
            .into_iter()
            .map(|client| client.join().unwrap())
            .collect::<Vec<_>>();
        let server_report = server_thread.join().unwrap();
        let (_, local_hash) = run_scripted_match(seed, ticks);
        assert_eq!(reports[0].last_input_sequence, ticks - 1);
        assert_eq!(reports[1].snapshots_received, ticks);
        assert_eq!(reports[0].final_report, reports[1].final_report);
        assert_eq!(reports[0].final_report, server_report);
        assert_eq!(server_report.state_hash, local_hash);
        assert!(
            reports
                .iter()
                .all(|report| report.observed_pre_explosion_constraints)
        );
        assert!(
            reports
                .iter()
                .all(|report| report.observed_post_explosion_release)
        );
        assert_eq!(
            server_report.dynamic_body_digest,
            dynamic_body_digest(&server_report.state)
        );
    }

    #[test]
    fn two_udp_clients_receive_the_same_complete_rematch_and_draft_flow() {
        let seed = rounds_sim::SOURCE_DRAFT_SEED;
        let ticks = rounds_sim::REMATCH_DRAFT_TICKS;
        let scripts = scripted_inputs_for(ReplayProfile::RematchDraftReplay, seed, ticks);
        let server = BoundServer::bind("127.0.0.1:0").unwrap();
        let address = server.local_addr().unwrap();
        let server_thread = thread::spawn(move || {
            server
                .run(seed, ticks, ReplayProfile::RematchDraftReplay)
                .unwrap()
        });
        let clients = scripts
            .into_iter()
            .enumerate()
            .map(|(client_id, inputs)| {
                thread::spawn(move || {
                    send_inputs(
                        address,
                        client_id as u8,
                        seed,
                        ReplayProfile::RematchDraftReplay,
                        &inputs,
                    )
                    .unwrap()
                })
            })
            .collect::<Vec<_>>();
        let reports = clients
            .into_iter()
            .map(|client| client.join().unwrap())
            .collect::<Vec<_>>();
        let server_report = server_thread.join().unwrap();
        assert_eq!(reports[0].final_report, reports[1].final_report);
        assert_eq!(reports[0].final_report, server_report);
        assert_eq!(
            reports[0].observed_flow_phases,
            reports[1].observed_flow_phases
        );
        assert_eq!(
            reports[0].observed_flow_phases,
            vec![
                FlowPhase::CombatConclusion,
                FlowPhase::RematchPrompt,
                FlowPhase::ArenaFade,
                FlowPhase::Draft,
                FlowPhase::Reveal,
                FlowPhase::Handoff,
                FlowPhase::Draft,
                FlowPhase::Reveal,
                FlowPhase::ArenaTransition,
                FlowPhase::ResumedCombat,
            ]
        );
        assert!(reports.iter().all(|report| {
            report.observed_source_terminal_state
                && report.observed_rematch_reset
                && report.observed_blue_fan_by_tick_960
        }));
        assert!(server_report.flow_digest.is_some());
        assert!(server_report.loadout_digest.is_some());
    }

    #[test]
    fn two_udp_clients_observe_radial_motion_damage_and_half_blue() {
        let seed = 42;
        let profile = ReplayProfile::RadialSawHalfBlueReplay;
        let ticks = rounds_sim::RADIAL_REPLAY_TICKS;
        let scripts = scripted_inputs_for(profile, seed, ticks);
        let server = BoundServer::bind("127.0.0.1:0").unwrap();
        let address = server.local_addr().unwrap();
        let server_thread = thread::spawn(move || server.run(seed, ticks, profile).unwrap());
        let clients = scripts
            .into_iter()
            .enumerate()
            .map(|(client_id, inputs)| {
                thread::spawn(move || {
                    send_inputs(address, client_id as u8, seed, profile, &inputs).unwrap()
                })
            })
            .collect::<Vec<_>>();
        let reports = clients
            .into_iter()
            .map(|client| client.join().unwrap())
            .collect::<Vec<_>>();
        let server_report = server_thread.join().unwrap();

        assert_eq!(reports[0].final_report, reports[1].final_report);
        assert_eq!(reports[0].final_report, server_report);
        assert!(reports.iter().all(|report| {
            report.observed_radial_saw_motion
                && report.observed_radial_damage
                && report.observed_radial_result_onset
                && report.observed_radial_half_blue
        }));
        assert_eq!(server_report.state.round.as_ref().unwrap().scores, [1, 1]);
        assert_eq!(server_report.state.metrics.hits, 1);
        assert!(server_report.arena_digest.len() == 64);
        assert!(server_report.saw_digest.len() == 64);
        assert!(server_report.combat_digest.len() == 64);
        assert_eq!(server_report.round_digest.as_ref().unwrap().len(), 64);
    }
}
