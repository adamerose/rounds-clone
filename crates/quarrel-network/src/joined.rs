use crate::Transport;
use std::io;
use std::sync::atomic::{AtomicBool, Ordering};

/// Two transports as one, so an authority can hear its own fighter in memory and a friend over
/// another network.
pub struct Joined<A, B> {
    pub first: A,
    pub second: B,
    second_next: AtomicBool,
}

impl<A, B> Joined<A, B> {
    pub fn new(first: A, second: B) -> Self {
        Self {
            first,
            second,
            second_next: AtomicBool::new(false),
        }
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum JoinedPeer<A, B> {
    First(A),
    Second(B),
}

impl<A: Transport, B: Transport> Transport for Joined<A, B> {
    type Peer = JoinedPeer<A::Peer, B::Peer>;
    fn send_to(&self, bytes: &[u8], peer: Self::Peer) -> io::Result<usize> {
        match peer {
            JoinedPeer::First(peer) => self.first.send_to(bytes, peer),
            JoinedPeer::Second(peer) => self.second.send_to(bytes, peer),
        }
    }
    /// Takes turns, so one call waits on only one transport and neither starves the other.
    fn recv_from(&self, bytes: &mut [u8]) -> io::Result<(usize, Self::Peer)> {
        if self.second_next.fetch_xor(true, Ordering::Relaxed) {
            let (size, peer) = self.second.recv_from(bytes)?;
            Ok((size, JoinedPeer::Second(peer)))
        } else {
            let (size, peer) = self.first.recv_from(bytes)?;
            Ok((size, JoinedPeer::First(peer)))
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::{LiveClient, LiveServer, MemoryNetwork, NetworkConditions};
    use quarrel_sim::MatchConfig;
    use std::thread;

    /// The Steam host's shape, with memory standing in for Steam: the authority hears its own
    /// fighter in memory and the friend over a second network with an 80 ms round trip and 2 % loss.
    #[test]
    fn joined_authority_plays_a_local_and_a_remote_fighter() {
        let config = MatchConfig {
            target_score: 1,
            ..Default::default()
        };
        let impaired = NetworkConditions {
            delay_ms: 40,
            loss_basis_points: 200,
            seed: 84,
            ..Default::default()
        };
        let (own, friend) = (MemoryNetwork::default(), MemoryNetwork::default());
        let authority = Joined::new(own.endpoint(), friend.endpoint());
        let links = [
            (
                own.endpoint(),
                authority.first.peer(),
                NetworkConditions::default(),
            ),
            (friend.endpoint(), authority.second.peer(), impaired),
        ];
        let server = LiveServer::over(authority, impaired).unwrap();
        let server_config = config.clone();
        let server = thread::spawn(move || server.run(server_config, 240, None).unwrap());
        let clients = links
            .into_iter()
            .enumerate()
            .map(|(id, (link, authority, conditions))| {
                let client =
                    LiveClient::over(link, authority, id as u8, config.clone(), conditions)
                        .unwrap();
                thread::spawn(move || client.run(240).unwrap())
            })
            .collect::<Vec<_>>();
        let server = server.join().unwrap();
        assert_eq!(server.result, "completed");
        assert_eq!(server.terminal_acks, [true, true]);
        assert!(server.traffic.dropped_datagrams > 0, "{:?}", server.traffic);
        for client in clients {
            let report = client.join().unwrap();
            assert_eq!(report.result, "completed");
            assert_eq!(report.last_tick, 240);
            assert_eq!(report.state_hash.as_ref(), Some(&server.state_hash));
        }
    }
}
