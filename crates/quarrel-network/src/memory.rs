//! In-process datagram links: a host's own fighter and transport tests use them.
use crate::Transport;
use std::collections::HashMap;
use std::io;
use std::sync::mpsc::{Receiver, RecvTimeoutError, Sender, channel};
use std::sync::{Arc, Mutex};
use std::time::Duration;

const WAIT: Duration = Duration::from_millis(5);

#[derive(Default)]
struct Inboxes {
    next: u32,
    senders: HashMap<u32, Sender<(u32, Vec<u8>)>>,
}

/// Endpoints made from one network deliver to each other by [`MemoryEndpoint::peer`].
#[derive(Clone, Default)]
pub struct MemoryNetwork(Arc<Mutex<Inboxes>>);

impl MemoryNetwork {
    pub fn endpoint(&self) -> MemoryEndpoint {
        let (sender, inbox) = channel();
        let mut inboxes = self.0.lock().unwrap();
        let id = inboxes.next;
        inboxes.next += 1;
        inboxes.senders.insert(id, sender);
        MemoryEndpoint {
            id,
            network: self.clone(),
            inbox,
        }
    }
}

pub struct MemoryEndpoint {
    id: u32,
    network: MemoryNetwork,
    inbox: Receiver<(u32, Vec<u8>)>,
}

impl MemoryEndpoint {
    pub fn peer(&self) -> u32 {
        self.id
    }
}

impl Drop for MemoryEndpoint {
    fn drop(&mut self) {
        self.network.0.lock().unwrap().senders.remove(&self.id);
    }
}

impl Transport for MemoryEndpoint {
    type Peer = u32;
    fn send_to(&self, bytes: &[u8], peer: u32) -> io::Result<usize> {
        // A datagram to a closed endpoint is lost, as it would be over UDP.
        if let Some(sender) = self.network.0.lock().unwrap().senders.get(&peer) {
            let _ = sender.send((self.id, bytes.to_vec()));
        }
        Ok(bytes.len())
    }
    fn recv_from(&self, bytes: &mut [u8]) -> io::Result<(usize, u32)> {
        match self.inbox.recv_timeout(WAIT) {
            Ok((sender, datagram)) => {
                let size = datagram.len().min(bytes.len());
                bytes[..size].copy_from_slice(&datagram[..size]);
                Ok((size, sender))
            }
            Err(RecvTimeoutError::Timeout | RecvTimeoutError::Disconnected) => {
                Err(io::ErrorKind::TimedOut.into())
            }
        }
    }
}
