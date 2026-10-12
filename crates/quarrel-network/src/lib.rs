use quarrel_sim::{
    AuthoritativeMatch, FlowPhase, MatchConfig, MatchSnapshot, PlayerInput, hash_snapshot,
};
use serde::{Deserialize, Serialize};
use sha2::{Digest, Sha256};
use std::io;
use std::net::{IpAddr, Ipv4Addr, Ipv6Addr, SocketAddr, ToSocketAddrs, UdpSocket};
use std::time::Duration;
mod presentation;
pub use presentation::{ClientPresentation, PresentationSample};

pub const NETWORK_PROTOCOL: u16 = 15;
pub const MAX_NETWORK_TICKS: u32 = 6_000;
const MAX_DATAGRAM: usize = 65_507;

fn bind_client_socket(authority: SocketAddr) -> io::Result<UdpSocket> {
    UdpSocket::bind(client_bind_address(authority))
}

/// Local play and tests must not listen on LAN interfaces; remote play needs every interface.
fn client_bind_address(authority: SocketAddr) -> SocketAddr {
    let ip: IpAddr = match (authority.is_ipv4(), authority.ip().is_loopback()) {
        (true, true) => Ipv4Addr::LOCALHOST.into(),
        (true, false) => Ipv4Addr::UNSPECIFIED.into(),
        (false, true) => Ipv6Addr::LOCALHOST.into(),
        (false, false) => Ipv6Addr::UNSPECIFIED.into(),
    };
    SocketAddr::new(ip, 0)
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
pub use conditions::{NetworkConditions, NetworkTraffic, Transport};
mod joined;
pub use joined::{Joined, JoinedPeer};
mod memory;
pub use memory::{MemoryEndpoint, MemoryNetwork};

#[cfg(test)]
mod tests {
    use super::*;

    type Script = Box<dyn FnOnce(SocketAddr, &[UdpSocket; 2])>;

    // Every endpoint a test sends to stays bound until the test ends, so no packet can
    // reach a port that another parallel test bound after this one released it.
    fn endpoint() -> UdpSocket {
        let socket = UdpSocket::bind("127.0.0.1:0").unwrap();
        socket
            .set_read_timeout(Some(Duration::from_secs(10)))
            .unwrap();
        socket
    }
    fn hello(client_id: u8, config: &MatchConfig) -> ClientPacket {
        ClientPacket::Hello {
            protocol: NETWORK_PROTOCOL,
            client_id,
            config: config.clone(),
        }
    }
    fn input(client_id: u8, sequence: u32) -> ClientPacket {
        ClientPacket::Input {
            protocol: NETWORK_PROTOCOL,
            client_id,
            sequence,
            input: PlayerInput::default(),
        }
    }
    /// Runs a two-fighter authority while `clients` sends packets from two sockets the
    /// test owns, and returns the authority's result.
    fn authority_result(
        clients: impl FnOnce(SocketAddr, &[UdpSocket; 2]),
    ) -> Result<ServerReport, String> {
        let server = BoundServer::bind("127.0.0.1:0").unwrap();
        let address = server.local_addr().unwrap();
        let sockets = [endpoint(), endpoint()];
        let authority = std::thread::spawn(move || server.run(MatchConfig::default(), 5));
        clients(address, &sockets);
        authority.join().unwrap()
    }
    fn handshake(address: SocketAddr, sockets: &[UdpSocket; 2]) {
        for (id, socket) in sockets.iter().enumerate() {
            send_client(socket, address, &hello(id as u8, &MatchConfig::default())).unwrap();
        }
        for socket in sockets {
            assert!(matches!(
                receive_authority(socket, address).unwrap(),
                AuthorityPacket::Welcome { .. }
            ));
        }
    }

    #[test]
    fn two_scripted_clients_receive_the_authority_state_for_every_input() {
        let config = MatchConfig::default();
        let server = BoundServer::bind("127.0.0.1:0").unwrap();
        let address = server.local_addr().unwrap();
        let authority = std::thread::spawn({
            let config = config.clone();
            move || server.run(config, 3)
        });
        let clients = (0..2)
            .map(|id| {
                let config = config.clone();
                std::thread::spawn(move || {
                    send_inputs(address, id, config, &[PlayerInput::default(); 3])
                })
            })
            .collect::<Vec<_>>();
        let reports = clients
            .into_iter()
            .map(|client| client.join().unwrap().unwrap())
            .collect::<Vec<_>>();
        let server = authority.join().unwrap().unwrap();
        let mut local = AuthoritativeMatch::with_config(config).unwrap();
        for _ in 0..3 {
            local.step(&[PlayerInput::default(); 2]);
        }
        assert_eq!(server.state_hash, hash_snapshot(&local.snapshot()));
        assert_eq!((server.inputs_received, server.last_snapshot_tick), (6, 3));
        for report in reports {
            assert_eq!(report.last_input_sequence, 2);
            assert_eq!(report.snapshots_received, 3);
            assert_eq!(report.final_report.state_hash, server.state_hash);
        }
    }

    #[test]
    fn authority_rejects_bad_handshakes() {
        let other = MatchConfig {
            seed: MatchConfig::default().seed + 1,
            ..Default::default()
        };
        let cases: Vec<(Script, String)> = vec![
            (
                Box::new(|address, sockets| {
                    let mut old = hello(0, &MatchConfig::default());
                    if let ClientPacket::Hello { protocol, .. } = &mut old {
                        *protocol -= 1;
                    }
                    send_client(&sockets[0], address, &old).unwrap();
                }),
                format!("protocol {} is not supported", NETWORK_PROTOCOL - 1),
            ),
            (
                Box::new(move |address, sockets| {
                    send_client(&sockets[0], address, &hello(0, &other)).unwrap();
                }),
                "client 0 used a different match config".into(),
            ),
            (
                Box::new(|address, sockets| {
                    send_client(&sockets[0], address, &hello(2, &MatchConfig::default())).unwrap();
                }),
                "client id 2 is outside configured fighter count".into(),
            ),
            (
                Box::new(|address, sockets| {
                    for socket in sockets {
                        send_client(socket, address, &hello(0, &MatchConfig::default())).unwrap();
                    }
                }),
                "duplicate handshake for client 0".into(),
            ),
            (
                Box::new(|address, sockets| {
                    send_client(&sockets[0], address, &input(0, 0)).unwrap();
                }),
                "input arrived before all handshakes".into(),
            ),
        ];
        for (script, expected) in cases {
            assert_eq!(authority_result(script), Err(expected.clone()));
        }
    }

    #[test]
    fn authority_rejects_out_of_order_duplicate_and_misaddressed_inputs() {
        let cases: Vec<(Script, &str)> = vec![
            (
                Box::new(|address, sockets| {
                    send_client(&sockets[0], address, &input(0, 1)).unwrap();
                }),
                "client 0 sent sequence 1; expected 0",
            ),
            (
                Box::new(|address, sockets| {
                    send_client(&sockets[0], address, &input(0, 0)).unwrap();
                    send_client(&sockets[0], address, &input(0, 0)).unwrap();
                }),
                "duplicate input sequence 0 from client 0",
            ),
            (
                Box::new(|address, sockets| {
                    send_client(&sockets[1], address, &input(0, 0)).unwrap();
                }),
                "input sender did not match its handshake",
            ),
            (
                Box::new(|address, sockets| {
                    send_client(&sockets[0], address, &hello(0, &MatchConfig::default())).unwrap();
                }),
                "handshake arrived after match start",
            ),
        ];
        for (script, expected) in cases {
            let result = authority_result(|address, sockets| {
                handshake(address, sockets);
                script(address, sockets);
            });
            assert_eq!(result, Err(expected.to_owned()));
        }
    }

    #[test]
    fn tick_counts_and_client_ids_are_checked_before_any_traffic() {
        let bounds = format!("tick count must be 1..={MAX_NETWORK_TICKS}");
        for ticks in [0, MAX_NETWORK_TICKS + 1] {
            let server = BoundServer::bind("127.0.0.1:0").unwrap();
            assert_eq!(
                server.run(MatchConfig::default(), ticks),
                Err(bounds.clone())
            );
        }
        // This endpoint never answers, so a client that sent anything would time out.
        let silent = endpoint();
        let address = silent.local_addr().unwrap();
        for count in [0, MAX_NETWORK_TICKS as usize + 1] {
            let inputs = vec![PlayerInput::default(); count];
            assert_eq!(
                send_inputs(address, 0, MatchConfig::default(), &inputs),
                Err(bounds.clone())
            );
        }
        assert_eq!(
            send_inputs(
                address,
                2,
                MatchConfig::default(),
                &[PlayerInput::default()]
            ),
            Err("client id is outside configured fighter count".into())
        );
    }

    #[test]
    fn clients_reject_invalid_welcomes_and_snapshots() {
        let config = MatchConfig::default();
        let mut game = AuthoritativeMatch::with_config(config.clone()).unwrap();
        let first = game.snapshot();
        let first_hash = hash_snapshot(&first);
        game.step(&[PlayerInput::default(); 2]);
        let next = game.snapshot();
        let next_hash = hash_snapshot(&next);
        let welcome = |client_id, state_hash: &str| AuthorityPacket::Welcome {
            protocol: NETWORK_PROTOCOL,
            client_id,
            state_hash: state_hash.to_owned(),
            state: Box::new(first.clone()),
        };
        let snapshot = |protocol, sequence, state_hash: &str| AuthorityPacket::Snapshot {
            protocol,
            sequence,
            state_hash: state_hash.to_owned(),
            state: Box::new(next.clone()),
        };
        let invalid_handshake = "authority returned an invalid handshake".to_owned();
        let cases = [
            (welcome(1, &first_hash), None, invalid_handshake.clone()),
            (welcome(0, &next_hash), None, invalid_handshake),
            (
                welcome(0, &first_hash),
                Some(snapshot(NETWORK_PROTOCOL - 1, 0, &next_hash)),
                format!("protocol {} is not supported", NETWORK_PROTOCOL - 1),
            ),
            (
                welcome(0, &first_hash),
                Some(snapshot(NETWORK_PROTOCOL, 1, &next_hash)),
                "authority snapshot sequence did not advance".to_owned(),
            ),
            (
                welcome(0, &first_hash),
                Some(snapshot(NETWORK_PROTOCOL, 0, &first_hash)),
                "authority snapshot hash did not match payload".to_owned(),
            ),
        ];
        for (welcome, snapshot, expected) in cases {
            let authority = endpoint();
            let address = authority.local_addr().unwrap();
            let client = std::thread::spawn({
                let config = config.clone();
                move || send_inputs(address, 0, config, &[PlayerInput::default(); 2])
            });
            let (packet, client_address) = receive_client(&authority).unwrap();
            assert!(matches!(packet, ClientPacket::Hello { client_id: 0, .. }));
            send_authority(&authority, client_address, &welcome).unwrap();
            if let Some(snapshot) = snapshot {
                let (packet, _) = receive_client(&authority).unwrap();
                assert!(matches!(packet, ClientPacket::Input { sequence: 0, .. }));
                send_authority(&authority, client_address, &snapshot).unwrap();
            }
            assert_eq!(client.join().unwrap(), Err(expected));
        }
    }

    #[test]
    fn client_address_matches_authority_family_and_limits_local_connections_to_loopback() {
        for (authority, expected) in [
            ("127.0.0.1:9", "127.0.0.1:0"),
            ("127.0.0.2:9", "127.0.0.1:0"),
            ("192.0.2.1:9", "0.0.0.0:0"),
            ("[::1]:9", "[::1]:0"),
            ("[2001:db8::1]:9", "[::]:0"),
        ] {
            let address = client_bind_address(authority.parse().unwrap());
            assert_eq!(address.to_string(), expected, "authority {authority}");
        }
    }

    #[test]
    fn local_client_sockets_bind_loopback() {
        // Binding a wildcard address here would open a firewall prompt for the test executable.
        for (authority, expected) in [("127.0.0.2:9", "127.0.0.1"), ("[::1]:9", "::1")] {
            let socket = bind_client_socket(authority.parse().unwrap()).unwrap();
            assert_eq!(socket.local_addr().unwrap().ip().to_string(), expected);
        }
    }
}
