use rounds_sim::{MatchSnapshot, PlayerInput, hash_snapshot};
use serde::{Deserialize, Serialize};
use std::io;
use std::net::{SocketAddr, ToSocketAddrs, UdpSocket};
use std::time::Duration;

pub const NETWORK_PROTOCOL: u16 = 1;
pub const MAX_NETWORK_TICKS: u32 = 300;
const MAX_DATAGRAM_BYTES: usize = 65_507;

#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
pub struct ClientScript {
    pub protocol: u16,
    pub client_id: u8,
    pub seed: u64,
    pub inputs: Vec<PlayerInput>,
}

#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
pub struct ServerReport {
    pub protocol: u16,
    pub state_hash: String,
    pub state: MatchSnapshot,
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

    pub fn run(self, expected_seed: u64, expected_ticks: u32) -> Result<ServerReport, String> {
        if expected_ticks == 0 || expected_ticks > MAX_NETWORK_TICKS {
            return Err(format!(
                "network tick count must be between 1 and {MAX_NETWORK_TICKS}"
            ));
        }
        let mut scripts: [Option<(SocketAddr, ClientScript)>; 2] = [None, None];
        let mut buffer = vec![0_u8; MAX_DATAGRAM_BYTES];
        while scripts.iter().any(Option::is_none) {
            let (length, sender) = self
                .socket
                .recv_from(&mut buffer)
                .map_err(|error| format!("receive client script: {error}"))?;
            let script: ClientScript = serde_json::from_slice(&buffer[..length])
                .map_err(|error| format!("decode client script: {error}"))?;
            validate_script(&script, expected_seed, expected_ticks)?;
            let slot = usize::from(script.client_id);
            if scripts[slot].is_some() {
                return Err(format!("duplicate script for client {}", script.client_id));
            }
            scripts[slot] = Some((sender, script));
        }

        let [client_zero, client_one] = scripts.map(Option::unwrap);
        let mut simulation = rounds_sim::AuthoritativeMatch::new(expected_seed);
        for tick in 0..expected_ticks as usize {
            simulation.step([client_zero.1.inputs[tick], client_one.1.inputs[tick]]);
        }
        let state = simulation.snapshot();
        let report = ServerReport {
            protocol: NETWORK_PROTOCOL,
            state_hash: hash_snapshot(&state),
            state,
        };
        let bytes = serde_json::to_vec(&report).map_err(|error| error.to_string())?;
        for address in [client_zero.0, client_one.0] {
            self.socket
                .send_to(&bytes, address)
                .map_err(|error| format!("send authoritative state: {error}"))?;
        }
        Ok(report)
    }
}

pub fn send_script(
    address: impl ToSocketAddrs,
    script: &ClientScript,
) -> Result<ServerReport, String> {
    let socket =
        UdpSocket::bind("127.0.0.1:0").map_err(|error| format!("bind client socket: {error}"))?;
    socket
        .set_read_timeout(Some(Duration::from_secs(10)))
        .map_err(|error| format!("set client timeout: {error}"))?;
    let bytes = serde_json::to_vec(script).map_err(|error| error.to_string())?;
    if bytes.len() > MAX_DATAGRAM_BYTES {
        return Err(format!("client script is too large: {} bytes", bytes.len()));
    }
    socket
        .send_to(&bytes, address)
        .map_err(|error| format!("send client script: {error}"))?;
    let mut response = vec![0_u8; MAX_DATAGRAM_BYTES];
    let (length, _) = socket
        .recv_from(&mut response)
        .map_err(|error| format!("receive authoritative state: {error}"))?;
    let report: ServerReport = serde_json::from_slice(&response[..length])
        .map_err(|error| format!("decode authoritative state: {error}"))?;
    if report.protocol != NETWORK_PROTOCOL {
        return Err(format!("unsupported server protocol {}", report.protocol));
    }
    if report.state_hash != hash_snapshot(&report.state) {
        return Err("server state hash did not match its state payload".to_owned());
    }
    Ok(report)
}

fn validate_script(
    script: &ClientScript,
    expected_seed: u64,
    expected_ticks: u32,
) -> Result<(), String> {
    if script.protocol != NETWORK_PROTOCOL {
        return Err(format!("unsupported client protocol {}", script.protocol));
    }
    if script.client_id > 1 {
        return Err(format!("client id {} is outside 0..=1", script.client_id));
    }
    if script.seed != expected_seed {
        return Err(format!("client {} used the wrong seed", script.client_id));
    }
    if script.inputs.len() != expected_ticks as usize {
        return Err(format!(
            "client {} supplied {} inputs for {expected_ticks} ticks",
            script.client_id,
            script.inputs.len()
        ));
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;
    use rounds_sim::{run_scripted_match, scripted_inputs};
    use std::thread;

    #[test]
    fn udp_server_and_two_clients_agree_with_local_authority() {
        let seed = 38;
        let ticks = 120;
        let scripts = scripted_inputs(seed, ticks);
        let server = BoundServer::bind("127.0.0.1:0").unwrap();
        let address = server.local_addr().unwrap();
        let server_thread = thread::spawn(move || server.run(seed, ticks).unwrap());
        let clients = scripts
            .into_iter()
            .enumerate()
            .map(|(client_id, inputs)| {
                thread::spawn(move || {
                    send_script(
                        address,
                        &ClientScript {
                            protocol: NETWORK_PROTOCOL,
                            client_id: client_id as u8,
                            seed,
                            inputs,
                        },
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
        let (_, local_hash) = run_scripted_match(seed, ticks);
        assert_eq!(reports[0], reports[1]);
        assert_eq!(reports[0], server_report);
        assert_eq!(server_report.state_hash, local_hash);
    }
}
