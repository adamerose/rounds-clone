//! Outbound impairment: configure every endpoint to impair both directions once.
use serde::{Deserialize, Serialize};
use std::io;
use std::net::{SocketAddr, UdpSocket};
use std::sync::Mutex;
use std::time::{Duration, Instant};

#[derive(Clone, Copy, Debug, Default, Deserialize, Serialize)]
#[serde(default, deny_unknown_fields)]
pub struct NetworkConditions {
    /// One-way base delay; use 40 for an 80 ms base round trip.
    pub delay_ms: u32,
    /// Uniform, independent per-packet variation in [-jitter_ms, +jitter_ms].
    /// Negative resulting delays are clamped to zero. Packets can reorder.
    pub jitter_ms: u32,
    /// Drop probability in basis points (200 = 2%).
    pub loss_basis_points: u16,
    pub seed: u64,
}

impl NetworkConditions {
    pub fn validate(self) -> Result<Self, String> {
        if self.delay_ms > 10_000 || self.jitter_ms > 10_000 || self.loss_basis_points > 10_000 {
            return Err("network conditions require delay/jitter <= 10000 ms and loss <= 10000 basis points".into());
        }
        Ok(self)
    }

    /// JSON config for manual play, e.g. QUARREL_NETWORK_CONDITIONS=
    /// {"delay_ms":40,"jitter_ms":20,"loss_basis_points":200,"seed":88}.
    pub fn from_env() -> Result<Self, String> {
        match std::env::var("QUARREL_NETWORK_CONDITIONS") {
            Ok(value) => serde_json::from_str::<Self>(&value)
                .map_err(|error| format!("QUARREL_NETWORK_CONDITIONS: {error}"))?
                .validate(),
            Err(std::env::VarError::NotPresent) => Ok(Self::default()),
            Err(error) => Err(format!("QUARREL_NETWORK_CONDITIONS: {error}")),
        }
    }
}

// Raw UDP implements the same two operations for protocol-level tests.
pub(crate) trait DatagramSocket {
    fn send_to(&self, bytes: &[u8], address: SocketAddr) -> io::Result<usize>;
    fn recv_from(&self, bytes: &mut [u8]) -> io::Result<(usize, SocketAddr)>;
}
impl DatagramSocket for UdpSocket {
    fn send_to(&self, bytes: &[u8], address: SocketAddr) -> io::Result<usize> {
        UdpSocket::send_to(self, bytes, address)
    }
    fn recv_from(&self, bytes: &mut [u8]) -> io::Result<(usize, SocketAddr)> {
        UdpSocket::recv_from(self, bytes)
    }
}

struct Pending {
    due: Instant,
    address: SocketAddr,
    bytes: Vec<u8>,
}
#[derive(Clone, Copy, Debug, Default, Deserialize, Serialize)]
pub struct NetworkTraffic {
    pub sent_bytes: u64,
    pub received_bytes: u64,
    pub dropped_datagrams: u64,
}

struct Queue {
    traffic: NetworkTraffic,
    random: u64,
    packets: Vec<Pending>,
}
pub(crate) struct ConditionedSocket {
    pub socket: UdpSocket,
    conditions: NetworkConditions,
    queue: Mutex<Queue>,
}
impl ConditionedSocket {
    pub fn new(socket: UdpSocket, conditions: NetworkConditions, stream: u64) -> Self {
        Self {
            socket,
            conditions,
            queue: Mutex::new(Queue {
                traffic: NetworkTraffic::default(),
                random: conditions.seed.wrapping_add(stream),
                packets: Vec::new(),
            }),
        }
    }
    pub fn traffic(&self) -> NetworkTraffic {
        self.queue.lock().unwrap().traffic
    }
    fn flush(&self, queue: &mut Queue) -> io::Result<()> {
        // Sort by delivery time, allowing jitter to reorder datagrams.
        queue.packets.sort_by_key(|packet| packet.due);
        while queue
            .packets
            .first()
            .is_some_and(|packet| packet.due <= Instant::now())
        {
            let packet = &queue.packets[0];
            let size = self.socket.send_to(&packet.bytes, packet.address)?;
            queue.traffic.sent_bytes += size as u64;
            queue.packets.remove(0);
        }
        Ok(())
    }
    /// Deliver queued terminal acknowledgements / Leave before the socket closes.
    pub fn drain(&self) -> io::Result<()> {
        let mut queue = self.queue.lock().unwrap();
        while !queue.packets.is_empty() {
            self.flush(&mut queue)?;
            if let Some(packet) = queue.packets.first() {
                std::thread::sleep(packet.due.saturating_duration_since(Instant::now()));
            }
        }
        Ok(())
    }
}
fn random(state: &mut u64) -> u64 {
    // SplitMix64, with independent streams for the authority and each client.
    *state = state.wrapping_add(0x9e3779b97f4a7c15);
    let mut value = *state;
    value = (value ^ (value >> 30)).wrapping_mul(0xbf58476d1ce4e5b9);
    value = (value ^ (value >> 27)).wrapping_mul(0x94d049bb133111eb);
    value ^ (value >> 31)
}
impl DatagramSocket for ConditionedSocket {
    fn send_to(&self, bytes: &[u8], address: SocketAddr) -> io::Result<usize> {
        let mut queue = self.queue.lock().unwrap();
        self.flush(&mut queue)?;
        let conditions = self.conditions;
        if random(&mut queue.random) % 10_000 < u64::from(conditions.loss_basis_points) {
            queue.traffic.dropped_datagrams += 1;
            return Ok(bytes.len());
        }
        let jitter = random(&mut queue.random) % (2 * u64::from(conditions.jitter_ms) + 1);
        let delay = (i64::from(conditions.delay_ms) + jitter as i64
            - i64::from(conditions.jitter_ms))
        .max(0);
        if delay == 0 {
            let size = self.socket.send_to(bytes, address)?;
            queue.traffic.sent_bytes += size as u64;
            return Ok(size);
        }
        if queue.packets.len() >= 4096 {
            return Err(io::Error::other(
                "network conditions queue exceeds 4096 datagrams",
            ));
        }
        queue.packets.push(Pending {
            due: Instant::now() + Duration::from_millis(delay as u64),
            address,
            bytes: bytes.to_vec(),
        });
        Ok(bytes.len())
    }
    fn recv_from(&self, bytes: &mut [u8]) -> io::Result<(usize, SocketAddr)> {
        self.flush(&mut self.queue.lock().unwrap())?;
        let result = self.socket.recv_from(bytes)?;
        self.queue.lock().unwrap().traffic.received_bytes += result.0 as u64;
        Ok(result)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn udp_delivery_observes_delay_jitter_and_loss() {
        let receiver = UdpSocket::bind("127.0.0.1:0").unwrap();
        receiver.set_nonblocking(true).unwrap();
        let sender = ConditionedSocket::new(
            UdpSocket::bind("127.0.0.1:0").unwrap(),
            NetworkConditions {
                delay_ms: 100,
                jitter_ms: 20,
                loss_basis_points: 200,
                seed: 88,
            },
            0,
        );
        let start = Instant::now();
        for id in 0_u32..1000 {
            sender
                .send_to(&id.to_le_bytes(), receiver.local_addr().unwrap())
                .unwrap();
        }
        let mut arrivals = Vec::new();
        let until = start + Duration::from_millis(250);
        while Instant::now() < until {
            sender.flush(&mut sender.queue.lock().unwrap()).unwrap();
            let mut bytes = [0; 4];
            while receiver.recv_from(&mut bytes).is_ok() {
                arrivals.push(start.elapsed().as_millis());
            }
            std::thread::sleep(Duration::from_millis(1));
        }
        assert!(
            (950..995).contains(&arrivals.len()),
            "delivered {}",
            arrivals.len()
        );
        assert!(arrivals.iter().all(|ms| *ms >= 80 && *ms < 200));
        assert!(arrivals.iter().min().unwrap() < &100);
        assert!(arrivals.iter().max().unwrap() >= &120);
    }
    #[test]
    fn rejects_invalid_conditions() {
        assert!(
            NetworkConditions {
                loss_basis_points: 10_001,
                ..Default::default()
            }
            .validate()
            .is_err()
        );
        assert!(serde_json::from_str::<NetworkConditions>(r#"{"delay":40}"#).is_err());
    }
}
