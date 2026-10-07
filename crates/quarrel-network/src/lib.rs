use quarrel_sim::{
    AuthoritativeMatch, FlowPhase, MatchConfig, MatchSnapshot, PlayerInput, hash_snapshot,
};
use serde::{Deserialize, Serialize};
use sha2::{Digest, Sha256};
use std::io;
use std::net::{SocketAddr, ToSocketAddrs, UdpSocket};
use std::time::Duration;

pub const NETWORK_PROTOCOL: u16 = 12;
pub const MAX_NETWORK_TICKS: u32 = 6_000;
const MAX_DATAGRAM: usize = 65_507;

fn bind_client_socket(authority: SocketAddr) -> io::Result<UdpSocket> {
    // Local play and tests must not listen on LAN interfaces.
    let bind = match (authority.is_ipv4(), authority.ip().is_loopback()) {
        (true, true) => "127.0.0.1:0",
        (true, false) => "0.0.0.0:0",
        (false, true) => "[::1]:0",
        (false, false) => "[::]:0",
    };
    UdpSocket::bind(bind)
}

#[derive(Clone, Debug, Deserialize, Serialize)]
#[serde(tag = "kind", rename_all = "camelCase")]
enum ClientPacket {
    Hello {
        protocol: u16,
        client_id: u8,
        config: MatchConfig,
    },
    Input {
        protocol: u16,
        client_id: u8,
        sequence: u32,
        input: PlayerInput,
    },
}
#[derive(Clone, Debug, Deserialize, Serialize)]
#[serde(tag = "kind", rename_all = "camelCase")]
enum AuthorityPacket {
    Welcome {
        protocol: u16,
        client_id: u8,
        state_hash: String,
        state: Box<MatchSnapshot>,
    },
    Snapshot {
        protocol: u16,
        sequence: u32,
        state_hash: String,
        state: Box<MatchSnapshot>,
    },
}

#[derive(Clone, Debug, Deserialize, PartialEq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ServerReport {
    pub protocol: u16,
    pub config: MatchConfig,
    pub clients_handshaken: u8,
    pub inputs_received: u32,
    pub progressive_snapshots: u32,
    pub first_snapshot_tick: u32,
    pub last_snapshot_tick: u32,
    pub state_hash: String,
    pub state: MatchSnapshot,
}
#[derive(Clone, Debug, Deserialize, PartialEq, Serialize)]
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
    pub progressive_state_sha256: String,
    pub observed_opening_draft: bool,
    pub observed_loser_draft: bool,
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
    pub fn run(self, config: MatchConfig, ticks: u32) -> Result<ServerReport, String> {
        validate_ticks(ticks)?;
        config.validate()?;
        let count = config.fighter_count;
        let mut clients = vec![None; count];
        while clients.iter().any(Option::is_none) {
            let (packet, sender) = receive_client(&self.socket)?;
            let ClientPacket::Hello {
                protocol,
                client_id,
                config: received,
            } = packet
            else {
                return Err("input arrived before all handshakes".into());
            };
            validate_protocol(protocol)?;
            let id = usize::from(client_id);
            if id >= count {
                return Err(format!(
                    "client id {client_id} is outside configured fighter count"
                ));
            }
            if received != config {
                return Err(format!("client {client_id} used a different match config"));
            }
            if clients[id].replace(sender).is_some() {
                return Err(format!("duplicate handshake for client {client_id}"));
            }
        }
        let clients = clients.into_iter().map(Option::unwrap).collect::<Vec<_>>();
        let mut simulation = AuthoritativeMatch::with_config(config.clone())?;
        let initial = simulation.snapshot();
        let initial_hash = hash_snapshot(&initial);
        for (id, address) in clients.iter().enumerate() {
            send_authority(
                &self.socket,
                *address,
                &AuthorityPacket::Welcome {
                    protocol: NETWORK_PROTOCOL,
                    client_id: id as u8,
                    state_hash: initial_hash.clone(),
                    state: Box::new(initial.clone()),
                },
            )?;
        }
        let mut final_state = None;
        for sequence in 0..ticks {
            let mut inputs = vec![None; count];
            while inputs.iter().any(Option::is_none) {
                let (packet, sender) = receive_client(&self.socket)?;
                let ClientPacket::Input {
                    protocol,
                    client_id,
                    sequence: received,
                    input,
                } = packet
                else {
                    return Err("handshake arrived after match start".into());
                };
                validate_protocol(protocol)?;
                let id = usize::from(client_id);
                if id >= count || clients[id] != sender {
                    return Err("input sender did not match its handshake".into());
                }
                if received != sequence {
                    return Err(format!(
                        "client {client_id} sent sequence {received}; expected {sequence}"
                    ));
                }
                if inputs[id].replace(input.validated()).is_some() {
                    return Err(format!(
                        "duplicate input sequence {sequence} from client {client_id}"
                    ));
                }
            }
            simulation.step(&inputs.into_iter().map(Option::unwrap).collect::<Vec<_>>());
            let state = simulation.snapshot();
            let state_hash = hash_snapshot(&state);
            let packet = AuthorityPacket::Snapshot {
                protocol: NETWORK_PROTOCOL,
                sequence,
                state_hash: state_hash.clone(),
                state: Box::new(state.clone()),
            };
            for address in &clients {
                send_authority(&self.socket, *address, &packet)?;
            }
            final_state = Some((state, state_hash));
        }
        let (state, state_hash) = final_state.expect("validated tick count");
        Ok(ServerReport {
            protocol: NETWORK_PROTOCOL,
            config,
            clients_handshaken: count as u8,
            inputs_received: ticks * count as u32,
            progressive_snapshots: ticks,
            first_snapshot_tick: 1,
            last_snapshot_tick: state.tick,
            state_hash,
            state,
        })
    }
}

pub fn send_inputs(
    address: impl ToSocketAddrs,
    client_id: u8,
    config: MatchConfig,
    inputs: &[PlayerInput],
) -> Result<ClientSessionReport, String> {
    validate_ticks(inputs.len() as u32)?;
    config.validate()?;
    if usize::from(client_id) >= config.fighter_count {
        return Err("client id is outside configured fighter count".into());
    }
    let server = address
        .to_socket_addrs()
        .map_err(|e| format!("resolve authority: {e}"))?
        .next()
        .ok_or("authority address did not resolve")?;
    let socket = bind_client_socket(server).map_err(|e| format!("bind client socket: {e}"))?;
    socket
        .set_read_timeout(Some(Duration::from_secs(10)))
        .map_err(|e| e.to_string())?;
    send_client(
        &socket,
        server,
        &ClientPacket::Hello {
            protocol: NETWORK_PROTOCOL,
            client_id,
            config: config.clone(),
        },
    )?;
    let welcome = receive_authority(&socket, server)?;
    let AuthorityPacket::Welcome {
        protocol,
        client_id: id,
        state_hash,
        state,
    } = welcome
    else {
        return Err("authority returned an invalid handshake".into());
    };
    if protocol != NETWORK_PROTOCOL || id != client_id || state_hash != hash_snapshot(&state) {
        return Err("authority returned an invalid handshake".into());
    }
    let mut last = Some((*state, state_hash));
    let mut progressive = Sha256::new();
    let mut phases = Vec::new();
    let mut loser_draft = false;
    if let Some(flow) = last.as_ref().and_then(|(state, _)| state.flow.as_ref()) {
        phases.push(flow.phase);
    }
    for (sequence, input) in inputs.iter().copied().enumerate() {
        send_client(
            &socket,
            server,
            &ClientPacket::Input {
                protocol: NETWORK_PROTOCOL,
                client_id,
                sequence: sequence as u32,
                input: input
                    .with_progressive_observation(client_id, last.as_ref().map(|(state, _)| state)),
            },
        )?;
        let AuthorityPacket::Snapshot {
            protocol,
            sequence: received,
            state_hash,
            state,
        } = receive_authority(&socket, server)?
        else {
            return Err("authority repeated handshake".into());
        };
        validate_protocol(protocol)?;
        if received != sequence as u32 || state.tick != received + 1 {
            return Err("authority snapshot sequence did not advance".into());
        }
        if state_hash != hash_snapshot(&state) {
            return Err("authority snapshot hash did not match payload".into());
        }
        if let Some(flow) = &state.flow {
            if phases.last() != Some(&flow.phase) {
                phases.push(flow.phase);
            }
            loser_draft |= flow.phase == FlowPhase::Draft && flow.fight_number > 0;
        }
        progressive.update(state_hash.as_bytes());
        last = Some((*state, state_hash));
    }
    let (state, state_hash) = last.ok_or("client sent no inputs")?;
    state.flow.as_ref().ok_or("snapshot has no match flow")?;
    let final_report = ServerReport {
        protocol: NETWORK_PROTOCOL,
        config,
        clients_handshaken: 0,
        inputs_received: 0,
        progressive_snapshots: inputs.len() as u32,
        first_snapshot_tick: 1,
        last_snapshot_tick: state.tick,
        state_hash,
        state,
    };
    Ok(ClientSessionReport {
        protocol: NETWORK_PROTOCOL,
        client_id,
        handshake_complete: true,
        inputs_sent: inputs.len() as u32,
        first_input_sequence: 0,
        last_input_sequence: inputs.len().saturating_sub(1) as u32,
        snapshots_received: inputs.len() as u32,
        first_snapshot_tick: 0,
        last_snapshot_tick: final_report.last_snapshot_tick,
        progressive_state_sha256: format!("{:x}", progressive.finalize()),
        observed_opening_draft: phases.contains(&FlowPhase::Draft),
        observed_loser_draft: loser_draft,
        final_report,
    })
}
fn validate_ticks(ticks: u32) -> Result<(), String> {
    if (1..=MAX_NETWORK_TICKS).contains(&ticks) {
        Ok(())
    } else {
        Err(format!("tick count must be 1..={MAX_NETWORK_TICKS}"))
    }
}
fn validate_protocol(protocol: u16) -> Result<(), String> {
    if protocol == NETWORK_PROTOCOL {
        Ok(())
    } else {
        Err(format!("protocol {protocol} is not supported"))
    }
}
fn send_client(
    socket: &UdpSocket,
    address: SocketAddr,
    packet: &ClientPacket,
) -> Result<(), String> {
    send(socket, address, packet)
}
fn send_authority(
    socket: &UdpSocket,
    address: SocketAddr,
    packet: &AuthorityPacket,
) -> Result<(), String> {
    send(socket, address, packet)
}
fn send<T: Serialize>(socket: &UdpSocket, address: SocketAddr, packet: &T) -> Result<(), String> {
    let bytes = serde_json::to_vec(packet).map_err(|e| e.to_string())?;
    if bytes.len() > MAX_DATAGRAM {
        return Err("packet exceeds UDP datagram limit".into());
    }
    socket.send_to(&bytes, address).map_err(|e| e.to_string())?;
    Ok(())
}
fn receive_client(socket: &UdpSocket) -> Result<(ClientPacket, SocketAddr), String> {
    receive(socket)
}
fn receive_authority(socket: &UdpSocket, expected: SocketAddr) -> Result<AuthorityPacket, String> {
    let (packet, sender) = receive(socket)?;
    if sender == expected {
        Ok(packet)
    } else {
        Err("authority packet came from an unexpected address".into())
    }
}
fn receive<T: for<'a> Deserialize<'a>>(socket: &UdpSocket) -> Result<(T, SocketAddr), String> {
    let mut bytes = [0_u8; MAX_DATAGRAM];
    let (size, sender) = socket.recv_from(&mut bytes).map_err(|e| e.to_string())?;
    Ok((
        serde_json::from_slice(&bytes[..size]).map_err(|e| e.to_string())?,
        sender,
    ))
}

pub mod live;
pub use live::*;

mod conditions;
pub use conditions::{NetworkConditions, NetworkTraffic};

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn client_socket_matches_authority_family_and_limits_local_connections_to_loopback() {
        for (authority, expected) in [
            ("127.0.0.1:9", "127.0.0.1"),
            ("127.0.0.2:9", "127.0.0.1"),
            ("192.0.2.1:9", "0.0.0.0"),
            ("[::1]:9", "::1"),
            ("[2001:db8::1]:9", "::"),
        ] {
            let socket = bind_client_socket(authority.parse().unwrap()).unwrap();
            assert_eq!(socket.local_addr().unwrap().ip().to_string(), expected);
        }
    }
}
