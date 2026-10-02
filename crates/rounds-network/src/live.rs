use rounds_sim::{
    AuthoritativeMatch, FlowCommand, MatchSnapshot, PlayerInput, ReplayProfile, TICKS_PER_SECOND,
    hash_snapshot,
};
use serde::{Deserialize, Serialize};
use sha2::{Digest, Sha256};
use std::collections::VecDeque;
use std::fs;
use std::io;
use std::net::{SocketAddr, ToSocketAddrs, UdpSocket};
use std::path::Path;
use std::sync::atomic::{AtomicU64, Ordering};
use std::sync::{Arc, Mutex};
use std::time::{Duration, Instant, SystemTime, UNIX_EPOCH};

pub const MAX_LIVE_TICKS: u32 = 36_060;
const LIVE_PROTOCOL: u16 = 12;
const MAX_DATAGRAM: usize = 65_507;
const JOIN_WINDOW: Duration = Duration::from_secs(5);
const PEER_WINDOW: Duration = Duration::from_secs(3);
const TERMINAL_WINDOW: Duration = Duration::from_secs(2);
const SEND_INTERVAL: Duration = Duration::from_millis(16);
const TERMINAL_INTERVAL: Duration = Duration::from_millis(50);
const READ_INTERVAL: Duration = Duration::from_millis(5);
const EDGE_CAPACITY: usize = 8;
static NONCE: AtomicU64 = AtomicU64::new(0);

fn new_nonce() -> u64 {
    let clock = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .unwrap_or_default()
        .as_nanos();
    (clock as u64)
        ^ ((clock >> 64) as u64)
        ^ u64::from(std::process::id())
        ^ NONCE.fetch_add(1, Ordering::Relaxed)
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(tag = "kind", rename_all = "camelCase")]
enum ClientPacket {
    Hello {
        protocol: u16,
        client_id: u8,
        seed: u64,
        profile: ReplayProfile,
        nonce: u64,
    },
    Input {
        session: u64,
        client_id: u8,
        sequence: u64,
        held: PlayerInput,
        edge: Option<(u64, FlowCommand)>,
    },
    Ack {
        session: u64,
        client_id: u8,
        tick: u32,
        hash: String,
    },
    Leave {
        session: u64,
        client_id: u8,
    },
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(tag = "kind", rename_all = "camelCase")]
enum ServerPacket {
    Welcome {
        protocol: u16,
        client_id: u8,
        nonce: u64,
        session: u64,
    },
    Snapshot {
        session: u64,
        tick: u32,
        hash: String,
        ack: [u64; 2],
        state: Box<MatchSnapshot>,
    },
    Terminal {
        session: u64,
        tick: u32,
        hash: String,
        ack: [u64; 2],
        state: Box<MatchSnapshot>,
    },
    End {
        session: u64,
        reason: String,
    },
}

fn encode<T: Serialize>(packet: &T) -> Result<Vec<u8>, String> {
    let bytes =
        serde_json::to_vec(packet).map_err(|error| format!("encode live packet: {error}"))?;
    if bytes.len() > MAX_DATAGRAM {
        return Err(format!("live packet is too large: {} bytes", bytes.len()));
    }
    Ok(bytes)
}

fn send<T: Serialize>(
    socket: &UdpSocket,
    address: SocketAddr,
    packet: &T,
) -> Result<usize, String> {
    let bytes = encode(packet)?;
    socket
        .send_to(&bytes, address)
        .map_err(|error| format!("send live packet: {error}"))?;
    Ok(bytes.len())
}

fn recv<T: for<'a> Deserialize<'a>>(
    socket: &UdpSocket,
) -> Result<Option<(T, SocketAddr, usize)>, String> {
    let mut bytes = vec![0; MAX_DATAGRAM];
    match socket.recv_from(&mut bytes) {
        Ok((length, address)) => {
            let packet = serde_json::from_slice(&bytes[..length])
                .map_err(|error| format!("decode live packet: {error}"))?;
            Ok(Some((packet, address, length)))
        }
        Err(error)
            if matches!(
                error.kind(),
                io::ErrorKind::TimedOut | io::ErrorKind::WouldBlock
            ) =>
        {
            Ok(None)
        }
        // Windows reports an ICMP Port Unreachable without the peer address. Silence and Leave own peer loss.
        Err(error) if cfg!(windows) && error.raw_os_error() == Some(10054) => Ok(None),
        Err(error) => Err(format!("receive live packet: {error}")),
    }
}

fn valid_ticks(ticks: u32) -> Result<(), String> {
    if !(1..=MAX_LIVE_TICKS).contains(&ticks) {
        return Err(format!(
            "live tick count must be between 1 and {MAX_LIVE_TICKS}"
        ));
    }
    Ok(())
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct AppliedTick {
    pub tick: u32,
    pub elapsed_micros: u128,
    pub inputs: [PlayerInput; 2],
    pub hash: String,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct LiveServerReport {
    pub result: String,
    pub tick: u32,
    pub state_hash: String,
    pub terminal_acks: [bool; 2],
    pub mean_rate_hz: f64,
    pub late_ticks: u32,
    pub max_sent_datagram: usize,
    pub max_received_datagram: usize,
    pub elapsed_ms: u128,
    pub trace_sha256: String,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct LiveClientReport {
    pub result: String,
    pub client_id: u8,
    pub last_tick: u32,
    pub state_hash: Option<String>,
    pub received: Vec<(u32, String)>,
    pub max_sent_datagram: usize,
    pub max_received_datagram: usize,
}

pub struct LiveServer {
    socket: UdpSocket,
}

#[derive(Default)]
struct RunFaults {
    stall_at: Option<u32>,
    drop_terminal_for: Option<usize>,
    drop_final_snapshot_for: Option<usize>,
}

impl LiveServer {
    pub fn bind(address: impl ToSocketAddrs) -> Result<Self, String> {
        let socket =
            UdpSocket::bind(address).map_err(|error| format!("bind live authority: {error}"))?;
        socket
            .set_read_timeout(Some(READ_INTERVAL))
            .map_err(|error| error.to_string())?;
        Ok(Self { socket })
    }

    pub fn local_addr(&self) -> Result<SocketAddr, String> {
        self.socket.local_addr().map_err(|error| error.to_string())
    }

    pub fn run(
        self,
        seed: u64,
        ticks: u32,
        profile: ReplayProfile,
        trace_path: Option<&Path>,
    ) -> Result<LiveServerReport, String> {
        self.run_inner(seed, ticks, profile, trace_path, RunFaults::default())
    }

    fn run_inner(
        self,
        seed: u64,
        ticks: u32,
        profile: ReplayProfile,
        trace_path: Option<&Path>,
        faults: RunFaults,
    ) -> Result<LiveServerReport, String> {
        valid_ticks(ticks)?;
        let session = new_nonce();
        let mut peers: [Option<(SocketAddr, u64)>; 2] = [None, None];
        let join_until = Instant::now() + JOIN_WINDOW;
        let mut max_received = 0;
        let mut max_sent = 0;
        while peers.iter().any(Option::is_none) && Instant::now() < join_until {
            if let Some((packet, sender, size)) = recv::<ClientPacket>(&self.socket)? {
                max_received = max_received.max(size);
                if let ClientPacket::Hello {
                    protocol,
                    client_id,
                    seed: received_seed,
                    profile: received_profile,
                    nonce,
                } = packet
                {
                    if protocol != LIVE_PROTOCOL
                        || client_id > 1
                        || received_seed != seed
                        || received_profile != profile
                    {
                        continue;
                    }
                    let slot = &mut peers[usize::from(client_id)];
                    if slot.is_none() {
                        *slot = Some((sender, nonce));
                    }
                    if *slot == Some((sender, nonce)) {
                        max_sent = max_sent.max(send(
                            &self.socket,
                            sender,
                            &ServerPacket::Welcome {
                                protocol: LIVE_PROTOCOL,
                                client_id,
                                nonce,
                                session,
                            },
                        )?);
                    }
                }
            }
        }
        let Some([Some(peer0), Some(peer1)]) = Some(peers) else {
            return Err("join_timeout: two live peers did not join within 5 seconds".to_owned());
        };
        let peers = [peer0.0, peer1.0];
        let mut latest = [PlayerInput::default(); 2];
        let mut sequence = [0_u64; 2];
        let mut pending: [Option<(u64, FlowCommand)>; 2] = [None, None];
        let mut ack = [0_u64; 2];
        let mut last_seen = [Instant::now(); 2];
        let mut simulation = AuthoritativeMatch::new_with_profile(seed, profile);
        let start = Instant::now();
        let tick_period = Duration::from_secs_f64(1.0 / f64::from(TICKS_PER_SECOND));
        let mut next_tick = start + tick_period;
        let mut late_ticks = 0;
        let mut trace = Vec::with_capacity(ticks as usize);
        let mut final_state = None;
        for tick in 1..=ticks {
            if faults.stall_at == Some(tick) {
                std::thread::sleep(Duration::from_millis(150));
            }
            while Instant::now() < next_tick {
                if let Some((packet, sender, size)) = recv::<ClientPacket>(&self.socket)? {
                    max_received = max_received.max(size);
                    if let Some(id) = peers.iter().position(|peer| *peer == sender) {
                        match packet {
                            ClientPacket::Input {
                                session: received,
                                client_id,
                                sequence: received_sequence,
                                held,
                                edge,
                            } if received == session && usize::from(client_id) == id => {
                                last_seen[id] = Instant::now();
                                if received_sequence > sequence[id] {
                                    sequence[id] = received_sequence;
                                    latest[id] = PlayerInput {
                                        flow: None,
                                        ..held.validated()
                                    };
                                }
                                if let Some((edge_id, command)) = edge
                                    && edge_id == ack[id] + 1
                                    && pending[id].is_none()
                                {
                                    pending[id] = Some((edge_id, command));
                                }
                            }
                            ClientPacket::Leave {
                                session: received,
                                client_id,
                            } if received == session && usize::from(client_id) == id => {
                                return Err(format!("peer_left: client {id}"));
                            }
                            ClientPacket::Hello {
                                protocol,
                                client_id,
                                seed: received_seed,
                                profile: received_profile,
                                nonce,
                            } if protocol == LIVE_PROTOCOL
                                && usize::from(client_id) == id
                                && received_seed == seed
                                && received_profile == profile
                                && nonce == [peer0.1, peer1.1][id] =>
                            {
                                max_sent = max_sent.max(send(
                                    &self.socket,
                                    sender,
                                    &ServerPacket::Welcome {
                                        protocol: LIVE_PROTOCOL,
                                        client_id,
                                        nonce,
                                        session,
                                    },
                                )?);
                            }
                            _ => {}
                        }
                    }
                }
                if let Some(id) = last_seen
                    .iter()
                    .position(|seen| seen.elapsed() > PEER_WINDOW)
                {
                    return Err(format!(
                        "peer_silent: client {id} sent no valid input for 3 seconds"
                    ));
                }
            }
            let started_late = Instant::now() > next_tick + Duration::from_millis(2);
            let observation = simulation.snapshot();
            let inputs = std::array::from_fn(|id| {
                let mut input = latest[id];
                if let Some((edge_id, command)) = pending[id].take() {
                    input.flow = Some(command);
                    ack[id] = edge_id;
                }
                input
                    .with_progressive_observation(id as u8, Some(&observation))
                    .validated()
            });
            simulation.step(inputs);
            let state = simulation.snapshot();
            let hash = hash_snapshot(&state);
            trace.push(AppliedTick {
                tick,
                elapsed_micros: start.elapsed().as_micros(),
                inputs,
                hash: hash.clone(),
            });
            let packet = ServerPacket::Snapshot {
                session,
                tick,
                hash: hash.clone(),
                ack,
                state: Box::new(state.clone()),
            };
            for (id, peer) in peers.into_iter().enumerate() {
                if tick != ticks || faults.drop_final_snapshot_for != Some(id) {
                    max_sent = max_sent.max(send(&self.socket, peer, &packet)?);
                }
            }
            final_state = Some((state, hash));
            next_tick += tick_period;
            let overran = Instant::now() > next_tick;
            if started_late || overran {
                late_ticks += 1;
            }
            if overran {
                next_tick = Instant::now() + tick_period;
            }
        }
        let elapsed = start.elapsed();
        let (state, hash) = final_state.expect("positive tick count");
        let trace_bytes = serde_json::to_vec(&trace).map_err(|error| error.to_string())?;
        if let Some(path) = trace_path {
            fs::write(path, &trace_bytes)
                .map_err(|error| format!("write trace {}: {error}", path.display()))?;
        }
        let trace_sha256 = format!("{:x}", Sha256::digest(&trace_bytes));
        let mut terminal_acks = [false; 2];
        let terminal = ServerPacket::Terminal {
            session,
            tick: ticks,
            hash: hash.clone(),
            ack,
            state: Box::new(state),
        };
        let until = Instant::now() + TERMINAL_WINDOW;
        let mut resend = Instant::now();
        while Instant::now() < until && terminal_acks.iter().any(|acked| !acked) {
            if Instant::now() >= resend {
                for (id, peer) in peers.into_iter().enumerate() {
                    if !terminal_acks[id] && faults.drop_terminal_for != Some(id) {
                        max_sent = max_sent.max(send(&self.socket, peer, &terminal)?);
                    }
                }
                resend = Instant::now() + TERMINAL_INTERVAL;
            }
            if let Some((
                ClientPacket::Ack {
                    session: received,
                    client_id,
                    tick,
                    hash: received_hash,
                },
                sender,
                size,
            )) = recv::<ClientPacket>(&self.socket)?
            {
                max_received = max_received.max(size);
                if received == session
                    && tick == ticks
                    && received_hash == hash
                    && client_id < 2
                    && peers[usize::from(client_id)] == sender
                {
                    terminal_acks[usize::from(client_id)] = true;
                }
            }
        }
        Ok(LiveServerReport {
            result: if terminal_acks == [true; 2] {
                "completed"
            } else {
                "terminal_unacknowledged"
            }
            .to_owned(),
            tick: ticks,
            state_hash: hash,
            terminal_acks,
            mean_rate_hz: f64::from(ticks) / elapsed.as_secs_f64(),
            late_ticks,
            max_sent_datagram: max_sent,
            max_received_datagram: max_received,
            elapsed_ms: elapsed.as_millis(),
            trace_sha256,
        })
    }
}

struct Shared {
    held: PlayerInput,
    edges: VecDeque<(u64, FlowCommand)>,
    next_edge: u64,
    latest: Option<(MatchSnapshot, String)>,
    close: bool,
    result: Option<String>,
}

#[derive(Clone)]
pub struct LiveClientHandle(Arc<Mutex<Shared>>);

impl LiveClientHandle {
    pub fn set_held(&self, input: PlayerInput) {
        self.0.lock().unwrap().held = PlayerInput {
            flow: None,
            ..input.validated()
        };
    }
    pub fn push_flow(&self, flow: FlowCommand) -> Result<(), String> {
        let mut shared = self.0.lock().unwrap();
        if shared.edges.len() == EDGE_CAPACITY {
            return Err("flow_edge_overflow: eight commands are pending".to_owned());
        }
        shared.next_edge += 1;
        let id = shared.next_edge;
        shared.edges.push_back((id, flow));
        Ok(())
    }
    pub fn latest(&self) -> Option<(MatchSnapshot, String)> {
        self.0.lock().unwrap().latest.clone()
    }
    pub fn close(&self) {
        self.0.lock().unwrap().close = true;
    }
    pub fn result(&self) -> Option<String> {
        self.0.lock().unwrap().result.clone()
    }
}

pub struct LiveClient {
    socket: UdpSocket,
    authority: SocketAddr,
    client_id: u8,
    session: u64,
    handle: LiveClientHandle,
    max_sent: usize,
    max_received: usize,
}

impl LiveClient {
    pub fn connect(
        address: impl ToSocketAddrs,
        client_id: u8,
        seed: u64,
        profile: ReplayProfile,
    ) -> Result<Self, String> {
        if client_id > 1 {
            return Err("client id must be 0 or 1".to_owned());
        }
        let authority = address
            .to_socket_addrs()
            .map_err(|error| format!("resolve live authority: {error}"))?
            .find(SocketAddr::is_ipv4)
            .ok_or("live authority requires an IPv4 endpoint")?;
        let socket =
            UdpSocket::bind("0.0.0.0:0").map_err(|error| format!("bind live peer: {error}"))?;
        socket
            .set_read_timeout(Some(READ_INTERVAL))
            .map_err(|error| error.to_string())?;
        let nonce = new_nonce();
        let hello = ClientPacket::Hello {
            protocol: LIVE_PROTOCOL,
            client_id,
            seed,
            profile,
            nonce,
        };
        let until = Instant::now() + JOIN_WINDOW;
        let mut resend = Instant::now();
        let mut max_sent = 0;
        let mut max_received = 0;
        let session = loop {
            if Instant::now() >= until {
                return Err(
                    "join_timeout: authority did not welcome this peer within 5 seconds".to_owned(),
                );
            }
            if Instant::now() >= resend {
                max_sent = max_sent.max(send(&socket, authority, &hello)?);
                resend = Instant::now() + TERMINAL_INTERVAL;
            }
            if let Some((packet, sender, size)) = recv::<ServerPacket>(&socket)? {
                if sender != authority {
                    continue;
                }
                max_received = max_received.max(size);
                if let ServerPacket::Welcome {
                    protocol,
                    client_id: welcomed,
                    nonce: echoed,
                    session,
                } = packet
                    && protocol == LIVE_PROTOCOL
                    && welcomed == client_id
                    && echoed == nonce
                {
                    break session;
                }
            }
        };
        Ok(Self {
            socket,
            authority,
            client_id,
            session,
            handle: LiveClientHandle(Arc::new(Mutex::new(Shared {
                held: PlayerInput::default(),
                edges: VecDeque::new(),
                next_edge: 0,
                latest: None,
                close: false,
                result: None,
            }))),
            max_sent,
            max_received,
        })
    }

    pub fn handle(&self) -> LiveClientHandle {
        self.handle.clone()
    }

    pub fn run(mut self, ticks: u32) -> Result<LiveClientReport, String> {
        let handle = self.handle.clone();
        let result = self.run_inner(ticks);
        if let Err(error) = &result {
            handle.0.lock().unwrap().result = Some(format!("network_error: {error}"));
        }
        result
    }

    fn run_inner(&mut self, ticks: u32) -> Result<LiveClientReport, String> {
        valid_ticks(ticks)?;
        let mut last_tick = 0;
        let mut last_hash = None;
        let mut received = Vec::new();
        let mut sequence = 0;
        let mut next_send = Instant::now();
        let mut last_authority = Instant::now();
        loop {
            if self.handle.0.lock().unwrap().close {
                self.max_sent = self.max_sent.max(send(
                    &self.socket,
                    self.authority,
                    &ClientPacket::Leave {
                        session: self.session,
                        client_id: self.client_id,
                    },
                )?);
                return Ok(self.report("local_close", last_tick, last_hash, received));
            }
            if last_authority.elapsed() > PEER_WINDOW {
                return Ok(self.report("authority_silent", last_tick, last_hash, received));
            }
            if Instant::now() >= next_send {
                let (held, edge) = {
                    let shared = self.handle.0.lock().unwrap();
                    (shared.held, shared.edges.front().cloned())
                };
                sequence += 1;
                self.max_sent = self.max_sent.max(send(
                    &self.socket,
                    self.authority,
                    &ClientPacket::Input {
                        session: self.session,
                        client_id: self.client_id,
                        sequence,
                        held,
                        edge,
                    },
                )?);
                next_send = Instant::now() + SEND_INTERVAL;
            }
            if let Some((packet, sender, size)) = recv::<ServerPacket>(&self.socket)? {
                if sender != self.authority {
                    continue;
                }
                self.max_received = self.max_received.max(size);
                let terminal = matches!(&packet, ServerPacket::Terminal { .. });
                match packet {
                    ServerPacket::Snapshot {
                        session,
                        tick,
                        hash,
                        ack,
                        state,
                    }
                    | ServerPacket::Terminal {
                        session,
                        tick,
                        hash,
                        ack,
                        state,
                    } if session == self.session => {
                        if tick != state.tick || hash != hash_snapshot(&state) {
                            return Ok(self.report(
                                "validation_failure",
                                last_tick,
                                last_hash,
                                received,
                            ));
                        }
                        last_authority = Instant::now();
                        {
                            let mut shared = self.handle.0.lock().unwrap();
                            while shared
                                .edges
                                .front()
                                .is_some_and(|(id, _)| *id <= ack[usize::from(self.client_id)])
                            {
                                shared.edges.pop_front();
                            }
                            if tick > last_tick {
                                shared.latest = Some(((*state).clone(), hash.clone()));
                            }
                        }
                        if tick > last_tick {
                            last_tick = tick;
                            last_hash = Some(hash.clone());
                            received.push((tick, hash.clone()));
                        }
                        if terminal {
                            let acknowledgement = ClientPacket::Ack {
                                session: self.session,
                                client_id: self.client_id,
                                tick,
                                hash,
                            };
                            for _ in 0..4 {
                                self.max_sent = self.max_sent.max(send(
                                    &self.socket,
                                    self.authority,
                                    &acknowledgement,
                                )?);
                                std::thread::sleep(Duration::from_millis(10));
                            }
                            return Ok(self.report(
                                if tick == ticks {
                                    "completed"
                                } else {
                                    "early_terminal"
                                },
                                last_tick,
                                last_hash,
                                received,
                            ));
                        }
                    }
                    ServerPacket::End { session, reason } if session == self.session => {
                        return Ok(self.report(&reason, last_tick, last_hash, received));
                    }
                    _ => {}
                }
            }
        }
    }

    fn report(
        &self,
        result: &str,
        last_tick: u32,
        state_hash: Option<String>,
        received: Vec<(u32, String)>,
    ) -> LiveClientReport {
        self.handle.0.lock().unwrap().result = Some(result.to_owned());
        LiveClientReport {
            result: result.to_owned(),
            client_id: self.client_id,
            last_tick,
            state_hash,
            received,
            max_sent_datagram: self.max_sent,
            max_received_datagram: self.max_received,
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use rounds_sim::{ActionResult, FlowAction, FlowPhase};
    use std::thread;

    #[test]
    fn live_inputs_follow_observation_and_received_hashes_replay() {
        let seed = 38;
        let ticks = 90;
        let profile = ReplayProfile::TimberCollapseReplay;
        let server = LiveServer::bind("127.0.0.1:0").unwrap();
        let address = server.local_addr().unwrap();
        let trace_path = std::env::temp_dir().join(format!(
            "rounds-live-trace-{}-{}.json",
            std::process::id(),
            new_nonce()
        ));
        let server_trace = trace_path.clone();
        let authority = thread::spawn(move || {
            server
                .run(seed, ticks, profile, Some(&server_trace))
                .unwrap()
        });
        let clients = (0..2)
            .map(|id| {
                thread::spawn(move || {
                    let client = LiveClient::connect(address, id, seed, profile).unwrap();
                    let handle = client.handle();
                    let run = thread::spawn(move || client.run(ticks).unwrap());
                    while handle.latest().is_none_or(|(state, _)| state.tick < 20) {
                        thread::sleep(Duration::from_millis(2));
                    }
                    handle.set_held(PlayerInput {
                        move_axis: if id == 0 { 1 } else { -1 },
                        ..Default::default()
                    });
                    run.join().unwrap()
                })
            })
            .collect::<Vec<_>>();
        let reports = clients
            .into_iter()
            .map(|client| client.join().unwrap())
            .collect::<Vec<_>>();
        let server = authority.join().unwrap();
        assert_eq!(server.result, "completed");
        assert_eq!(server.terminal_acks, [true, true]);
        assert!(
            server.elapsed_ms >= 1_450,
            "authority tick clock ran too fast: {} ms",
            server.elapsed_ms
        );
        assert!(
            server.mean_rate_hz > 45.0,
            "unstalled live clock fell behind: {} Hz",
            server.mean_rate_hz
        );
        let trace: Vec<AppliedTick> =
            serde_json::from_slice(&fs::read(&trace_path).unwrap()).unwrap();
        fs::remove_file(trace_path).unwrap();
        let mut replay = AuthoritativeMatch::new_with_profile(seed, profile);
        for row in &trace {
            replay.step(row.inputs);
            assert_eq!(hash_snapshot(&replay.snapshot()), row.hash);
        }
        for (id, report) in reports.iter().enumerate() {
            let first_change = trace
                .iter()
                .find(|row| row.inputs[id].move_axis != 0)
                .unwrap()
                .tick;
            assert!(first_change > 20);
            assert!(
                trace
                    .iter()
                    .all(|row| row.tick >= first_change || row.inputs[id].move_axis == 0)
            );
            assert_eq!(report.result, "completed");
            assert_eq!(
                report.state_hash.as_deref(),
                Some(server.state_hash.as_str())
            );
            for (tick, hash) in &report.received {
                assert_eq!(trace[(*tick - 1) as usize].hash, *hash);
            }
        }
    }

    #[test]
    fn edge_queue_rejects_overflow_without_losing_order_or_held_updates() {
        let shared = LiveClientHandle(Arc::new(Mutex::new(Shared {
            held: PlayerInput::default(),
            edges: VecDeque::new(),
            next_edge: 0,
            latest: None,
            close: false,
            result: None,
        })));
        let command = FlowCommand {
            phase_revision: 0,
            action: rounds_sim::FlowAction::VoteYes,
        };
        for _ in 0..EDGE_CAPACITY {
            shared.push_flow(command).unwrap();
        }
        assert!(shared.push_flow(command).unwrap_err().contains("overflow"));
        shared.set_held(PlayerInput {
            move_axis: 1,
            ..Default::default()
        });
        let locked = shared.0.lock().unwrap();
        assert_eq!(locked.held.move_axis, 1);
        assert_eq!(
            locked.edges.iter().map(|(id, _)| *id).collect::<Vec<_>>(),
            (1..=8).collect::<Vec<_>>()
        );
    }

    #[test]
    fn full_live_bound_simulates_and_encodes_every_authoritative_state() {
        let mut simulation =
            AuthoritativeMatch::new_with_profile(59, ReplayProfile::LimeModularArenaReplay);
        let mut largest = 0;
        for tick in 1..=MAX_LIVE_TICKS {
            simulation.step([
                PlayerInput {
                    move_axis: if tick % 240 < 120 { 1 } else { -1 },
                    jump: tick % 180 < 24,
                    fire: tick % 60 < 30,
                    aim_at_opponent: true,
                    ..Default::default()
                },
                PlayerInput {
                    move_axis: if tick % 300 < 150 { -1 } else { 1 },
                    jump: tick % 210 < 24,
                    fire: tick % 75 < 30,
                    aim_at_opponent: true,
                    ..Default::default()
                },
            ]);
            let state = simulation.snapshot();
            let packet = ServerPacket::Snapshot {
                session: 1,
                tick,
                hash: hash_snapshot(&state),
                ack: [0, 0],
                state: Box::new(state),
            };
            largest = largest.max(encode(&packet).unwrap().len());
        }
        assert_eq!(simulation.snapshot().tick, MAX_LIVE_TICKS);
        println!("full-bound max encoded datagram: {largest} bytes");
    }

    #[test]
    fn flow_edges_are_consumed_fifo_even_when_rules_reject_them() {
        let seed = 41;
        let ticks = 460;
        let profile = ReplayProfile::RematchDraftReplay;
        let server = LiveServer::bind("127.0.0.1:0").unwrap();
        let address = server.local_addr().unwrap();
        let trace_path = std::env::temp_dir().join(format!(
            "rounds-flow-trace-{}-{}.json",
            std::process::id(),
            new_nonce()
        ));
        let server_trace = trace_path.clone();
        let authority = thread::spawn(move || {
            server
                .run(seed, ticks, profile, Some(&server_trace))
                .unwrap()
        });
        let first = thread::spawn(move || {
            let client = LiveClient::connect(address, 0, seed, profile).unwrap();
            let handle = client.handle();
            let run = thread::spawn(move || client.run(ticks).unwrap());
            wait_for(&handle, |state| {
                state
                    .flow
                    .as_ref()
                    .is_some_and(|flow| flow.phase == FlowPhase::RematchPrompt)
            });
            for revision in [0, 1, 1] {
                handle
                    .push_flow(FlowCommand {
                        phase_revision: revision,
                        action: FlowAction::VoteYes,
                    })
                    .unwrap();
            }
            wait_for(&handle, |_| handle.0.lock().unwrap().edges.is_empty());
            wait_for(&handle, |state| {
                state
                    .flow
                    .as_ref()
                    .is_some_and(|flow| flow.phase == FlowPhase::Draft)
            });
            let state = handle.latest().unwrap().0;
            let flow = state.flow.unwrap();
            let item = flow.offers[0][0];
            handle
                .push_flow(FlowCommand {
                    phase_revision: flow.phase_revision,
                    action: FlowAction::Hover(item),
                })
                .unwrap();
            handle
                .push_flow(FlowCommand {
                    phase_revision: flow.phase_revision,
                    action: FlowAction::Hover(item),
                })
                .unwrap();
            handle.set_held(PlayerInput {
                move_axis: 1,
                ..Default::default()
            });
            run.join().unwrap()
        });
        let second = thread::spawn(move || {
            let client = LiveClient::connect(address, 1, seed, profile).unwrap();
            let handle = client.handle();
            let run = thread::spawn(move || client.run(ticks).unwrap());
            wait_for(&handle, |state| {
                state
                    .flow
                    .as_ref()
                    .is_some_and(|flow| flow.phase == FlowPhase::RematchPrompt)
            });
            while handle
                .latest()
                .unwrap()
                .0
                .flow
                .as_ref()
                .unwrap()
                .rematch_votes[0]
                != rounds_sim::RematchVote::Yes
            {
                thread::sleep(Duration::from_millis(2));
            }
            thread::sleep(Duration::from_millis(80));
            handle
                .push_flow(FlowCommand {
                    phase_revision: 1,
                    action: FlowAction::VoteYes,
                })
                .unwrap();
            run.join().unwrap()
        });
        assert_eq!(first.join().unwrap().result, "completed");
        assert_eq!(second.join().unwrap().result, "completed");
        assert_eq!(authority.join().unwrap().result, "completed");
        let trace: Vec<AppliedTick> =
            serde_json::from_slice(&fs::read(&trace_path).unwrap()).unwrap();
        fs::remove_file(trace_path).unwrap();
        assert!(trace.iter().any(|row| row.inputs[0].move_axis == 1
            && matches!(
                row.inputs[0].flow,
                Some(FlowCommand {
                    action: FlowAction::Hover(_),
                    ..
                })
            )));
        let mut simulation = AuthoritativeMatch::new_with_profile(seed, profile);
        let mut seen = Vec::new();
        let mut edge_ticks = Vec::new();
        for row in trace {
            simulation.step(row.inputs);
            let state = simulation.snapshot();
            if let Some(command) = row.inputs[0].flow {
                edge_ticks.push(row.tick);
                seen.push((command.action, state.flow.unwrap().last_results[0]));
            }
        }
        assert_eq!(
            seen,
            vec![
                (FlowAction::VoteYes, ActionResult::Stale),
                (FlowAction::VoteYes, ActionResult::Accepted),
                (FlowAction::VoteYes, ActionResult::Duplicate),
                (
                    FlowAction::Hover(rounds_sim::ItemId::FrostSlam),
                    ActionResult::Accepted
                ),
                (
                    FlowAction::Hover(rounds_sim::ItemId::FrostSlam),
                    ActionResult::Duplicate
                ),
            ]
        );
        assert!(edge_ticks.windows(2).all(|pair| pair[0] < pair[1]));
    }

    #[test]
    fn stalled_authority_never_bursts_catch_up_ticks() {
        let server = LiveServer::bind("127.0.0.1:0").unwrap();
        let address = server.local_addr().unwrap();
        let trace_path = std::env::temp_dir().join(format!(
            "rounds-stall-{}-{}.json",
            std::process::id(),
            new_nonce()
        ));
        let server_trace = trace_path.clone();
        let authority = thread::spawn(move || {
            server
                .run_inner(
                    38,
                    80,
                    ReplayProfile::TimberCollapseReplay,
                    Some(&server_trace),
                    RunFaults {
                        stall_at: Some(30),
                        ..Default::default()
                    },
                )
                .unwrap()
        });
        let clients = (0..2)
            .map(|id| {
                thread::spawn(move || {
                    LiveClient::connect(address, id, 38, ReplayProfile::TimberCollapseReplay)
                        .unwrap()
                        .run(80)
                        .unwrap()
                })
            })
            .collect::<Vec<_>>();
        for client in clients {
            assert_eq!(client.join().unwrap().result, "completed");
        }
        let report = authority.join().unwrap();
        let trace: Vec<AppliedTick> =
            serde_json::from_slice(&fs::read(&trace_path).unwrap()).unwrap();
        fs::remove_file(trace_path).unwrap();
        assert!(report.late_ticks >= 1);
        assert!(trace[29].elapsed_micros - trace[28].elapsed_micros >= 140_000);
        assert!(trace[30].elapsed_micros - trace[29].elapsed_micros >= 10_000);
        assert!(report.elapsed_ms >= 1_300);
    }

    #[test]
    fn terminal_loss_and_local_close_are_bounded_and_ports_rebind() {
        let server = LiveServer::bind("127.0.0.1:0").unwrap();
        let address = server.local_addr().unwrap();
        let authority = thread::spawn(move || {
            server
                .run_inner(
                    38,
                    30,
                    ReplayProfile::TimberCollapseReplay,
                    None,
                    RunFaults {
                        drop_terminal_for: Some(1),
                        ..Default::default()
                    },
                )
                .unwrap()
        });
        let clients = (0..2)
            .map(|id| {
                thread::spawn(move || {
                    LiveClient::connect(address, id, 38, ReplayProfile::TimberCollapseReplay)
                        .unwrap()
                        .run(30)
                        .unwrap()
                })
            })
            .collect::<Vec<_>>();
        let reports = clients
            .into_iter()
            .map(|client| client.join().unwrap())
            .collect::<Vec<_>>();
        let server_report = authority.join().unwrap();
        assert_eq!(server_report.result, "terminal_unacknowledged");
        assert_eq!(server_report.terminal_acks, [true, false]);
        assert_eq!(reports[0].result, "completed");
        assert_eq!(reports[1].result, "authority_silent");
        assert_eq!(reports[1].last_tick, 30);
        let rebound = LiveServer::bind(address).unwrap();
        drop(rebound);

        let server = LiveServer::bind(address).unwrap();
        let authority =
            thread::spawn(move || server.run(38, 300, ReplayProfile::TimberCollapseReplay, None));
        let first = thread::spawn(move || {
            let client =
                LiveClient::connect(address, 0, 38, ReplayProfile::TimberCollapseReplay).unwrap();
            let handle = client.handle();
            let run = thread::spawn(move || client.run(300).unwrap());
            wait_for(&handle, |state| state.tick >= 20);
            handle.close();
            run.join().unwrap()
        });
        let second = thread::spawn(move || {
            LiveClient::connect(address, 1, 38, ReplayProfile::TimberCollapseReplay)
                .unwrap()
                .run(300)
                .unwrap()
        });
        assert_eq!(first.join().unwrap().result, "local_close");
        assert_eq!(second.join().unwrap().result, "authority_silent");
        let stopped = authority.join().unwrap().unwrap_err();
        assert!(
            stopped.starts_with("peer_left:") || stopped.starts_with("peer_silent:"),
            "{stopped}"
        );
        assert!(LiveServer::bind(address).is_ok());
    }

    #[test]
    fn terminal_state_recovers_a_lost_final_ordinary_snapshot() {
        let server = LiveServer::bind("127.0.0.1:0").unwrap();
        let address = server.local_addr().unwrap();
        let authority = thread::spawn(move || {
            server
                .run_inner(
                    38,
                    30,
                    ReplayProfile::TimberCollapseReplay,
                    None,
                    RunFaults {
                        drop_final_snapshot_for: Some(1),
                        ..Default::default()
                    },
                )
                .unwrap()
        });
        let clients = (0..2)
            .map(|id| {
                thread::spawn(move || {
                    LiveClient::connect(address, id, 38, ReplayProfile::TimberCollapseReplay)
                        .unwrap()
                        .run(30)
                        .unwrap()
                })
            })
            .collect::<Vec<_>>();
        let reports = clients
            .into_iter()
            .map(|client| client.join().unwrap())
            .collect::<Vec<_>>();
        let server = authority.join().unwrap();
        assert_eq!(server.result, "completed");
        assert_eq!(server.terminal_acks, [true, true]);
        for report in reports {
            assert_eq!(report.result, "completed");
            assert_eq!(report.last_tick, 30);
            assert_eq!(
                report.state_hash.as_deref(),
                Some(server.state_hash.as_str())
            );
        }
    }

    #[test]
    fn old_session_input_from_a_current_peer_is_ignored() {
        let server = LiveServer::bind("127.0.0.1:0").unwrap();
        let address = server.local_addr().unwrap();
        let trace_path = std::env::temp_dir().join(format!(
            "rounds-reuse-{}-{}.json",
            std::process::id(),
            new_nonce()
        ));
        let server_trace = trace_path.clone();
        let authority = thread::spawn(move || {
            server
                .run(
                    38,
                    3,
                    ReplayProfile::TimberCollapseReplay,
                    Some(&server_trace),
                )
                .unwrap()
        });
        let peers = (0..2_u8)
            .map(|id| {
                let socket = UdpSocket::bind("127.0.0.1:0").unwrap();
                socket
                    .set_read_timeout(Some(Duration::from_millis(200)))
                    .unwrap();
                let nonce = new_nonce();
                send(
                    &socket,
                    address,
                    &ClientPacket::Hello {
                        protocol: LIVE_PROTOCOL,
                        client_id: id,
                        seed: 38,
                        profile: ReplayProfile::TimberCollapseReplay,
                        nonce,
                    },
                )
                .unwrap();
                let session = loop {
                    if let Some((
                        ServerPacket::Welcome {
                            client_id,
                            nonce: echoed,
                            session,
                            ..
                        },
                        _,
                        _,
                    )) = recv::<ServerPacket>(&socket).unwrap()
                        && client_id == id
                        && echoed == nonce
                    {
                        break session;
                    }
                };
                (id, socket, session)
            })
            .collect::<Vec<_>>();
        let session = peers[0].2;
        assert_eq!(session, peers[1].2);
        send(
            &peers[0].1,
            address,
            &ClientPacket::Input {
                session: session ^ 1,
                client_id: 0,
                sequence: 99,
                held: PlayerInput {
                    move_axis: 1,
                    ..Default::default()
                },
                edge: None,
            },
        )
        .unwrap();
        for (id, socket, _) in &peers {
            send(
                socket,
                address,
                &ClientPacket::Input {
                    session,
                    client_id: *id,
                    sequence: 1,
                    held: PlayerInput::default(),
                    edge: None,
                },
            )
            .unwrap();
        }
        for (id, socket, _) in &peers {
            loop {
                if let Some((
                    ServerPacket::Terminal {
                        session: received,
                        tick,
                        hash,
                        ..
                    },
                    _,
                    _,
                )) = recv::<ServerPacket>(socket).unwrap()
                {
                    send(
                        socket,
                        address,
                        &ClientPacket::Ack {
                            session: received,
                            client_id: *id,
                            tick,
                            hash,
                        },
                    )
                    .unwrap();
                    break;
                }
            }
        }
        assert_eq!(authority.join().unwrap().result, "completed");
        let trace: Vec<AppliedTick> =
            serde_json::from_slice(&fs::read(&trace_path).unwrap()).unwrap();
        fs::remove_file(trace_path).unwrap();
        assert!(trace.iter().all(|row| row.inputs[0].move_axis == 0));
    }

    #[test]
    fn join_timeout_and_bad_state_are_named_failures() {
        let abandoned = UdpSocket::bind("127.0.0.1:0").unwrap();
        let absent = abandoned.local_addr().unwrap();
        drop(abandoned);
        let start = Instant::now();
        assert!(
            LiveClient::connect(absent, 0, 38, ReplayProfile::TimberCollapseReplay)
                .err()
                .unwrap()
                .starts_with("join_timeout:")
        );
        assert!(start.elapsed() >= JOIN_WINDOW);

        let fake = UdpSocket::bind("127.0.0.1:0").unwrap();
        let address = fake.local_addr().unwrap();
        fake.set_read_timeout(Some(Duration::from_millis(100)))
            .unwrap();
        let authority = thread::spawn(move || {
            let (
                ClientPacket::Hello {
                    client_id, nonce, ..
                },
                sender,
                _,
            ) = recv::<ClientPacket>(&fake).unwrap().unwrap()
            else {
                panic!("expected hello")
            };
            send(
                &fake,
                sender,
                &ServerPacket::Welcome {
                    protocol: LIVE_PROTOCOL,
                    client_id,
                    nonce,
                    session: 7,
                },
            )
            .unwrap();
            let mut simulation =
                AuthoritativeMatch::new_with_profile(38, ReplayProfile::TimberCollapseReplay);
            simulation.step([PlayerInput::default(); 2]);
            let state = simulation.snapshot();
            send(
                &fake,
                sender,
                &ServerPacket::Snapshot {
                    session: 7,
                    tick: 1,
                    hash: "invalid".to_owned(),
                    ack: [0, 0],
                    state: Box::new(state),
                },
            )
            .unwrap();
        });
        let client =
            LiveClient::connect(address, 0, 38, ReplayProfile::TimberCollapseReplay).unwrap();
        assert_eq!(client.run(3).unwrap().result, "validation_failure");
        authority.join().unwrap();
    }

    fn wait_for(handle: &LiveClientHandle, predicate: impl Fn(&MatchSnapshot) -> bool) {
        let until = Instant::now() + Duration::from_secs(8);
        while Instant::now() < until {
            if handle.latest().is_some_and(|(state, _)| predicate(&state)) {
                return;
            }
            thread::sleep(Duration::from_millis(2));
        }
        panic!("live state did not reach the expected phase");
    }
}
