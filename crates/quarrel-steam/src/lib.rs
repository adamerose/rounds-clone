//! Online play through Steam: a friends-only lobby, Steam's invite overlay and its relay network.
//!
//! Executables that link this crate load `steam_api64.dll` at startup, so it ships beside them.
use quarrel_network::Transport;
use std::collections::VecDeque;
use std::io;
use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::mpsc::{Receiver, channel};
use std::sync::{Arc, Mutex};
use std::thread::JoinHandle;
use std::time::{Duration, Instant};
use steamworks::networking_messages::NetworkingMessages;
use steamworks::networking_types::SendFlags;
use steamworks::{
    CallbackHandle, Client, FriendFlags, GameLobbyJoinRequested, GameRichPresenceJoinRequested,
    LobbyId, LobbyType, SteamId,
};

/// Valve's public test app, used until QUARREL has its own App ID.
pub const DEVELOPMENT_APP_ID: u32 = 480;
const CALLBACK_INTERVAL: Duration = Duration::from_millis(5);
const LOBBY_WINDOW: Duration = Duration::from_secs(20);
const OVERLAY_WINDOW: Duration = Duration::from_secs(30);
const RECEIVE_WAIT: Duration = Duration::from_millis(5);
const CHANNEL: u32 = 0;
/// The launch option Steam passes when a player joins a friend's lobby from outside the game.
pub const CONNECT_LOBBY: &str = "+connect_lobby";

fn needed(detail: impl std::fmt::Display) -> String {
    format!("Steam is needed for online play: start Steam and sign in, then try again. ({detail})")
}

/// The Steam App ID from `QUARREL_STEAM_APP_ID`, or the development app.
/// `QUARREL_STEAM=off` makes Steam unavailable, as when it is not running.
pub fn configured_app_id() -> Result<u32, String> {
    if std::env::var("QUARREL_STEAM").is_ok_and(|value| value == "off") {
        return Err(needed("QUARREL_STEAM=off"));
    }
    match std::env::var("QUARREL_STEAM_APP_ID") {
        Ok(value) => value
            .parse()
            .map_err(|error| format!("QUARREL_STEAM_APP_ID: {error}")),
        Err(std::env::VarError::NotPresent) => Ok(DEVELOPMENT_APP_ID),
        Err(error) => Err(format!("QUARREL_STEAM_APP_ID: {error}")),
    }
}

/// A signed-in Steam session. Callbacks run on a background thread until it drops.
pub struct Steam {
    client: Client,
    pumping: Arc<AtomicBool>,
    pump: Option<JoinHandle<()>>,
    lobby: Mutex<Option<LobbyId>>,
    callbacks: Mutex<Vec<CallbackHandle>>,
}

impl Steam {
    pub fn start() -> Result<Self, String> {
        let client = Client::init_app(configured_app_id()?).map_err(needed)?;
        if !client.user().logged_on() {
            return Err(needed("not signed in"));
        }
        // Measure relay routes now, so the first match does not wait for them.
        client.networking_utils().init_relay_network_access();
        let pumping = Arc::new(AtomicBool::new(true));
        let pump = {
            let (client, pumping) = (client.clone(), pumping.clone());
            std::thread::spawn(move || {
                while pumping.load(Ordering::Relaxed) {
                    client.run_callbacks();
                    std::thread::sleep(CALLBACK_INTERVAL);
                }
            })
        };
        Ok(Self {
            client,
            pumping,
            pump: Some(pump),
            lobby: Mutex::new(None),
            callbacks: Mutex::new(Vec::new()),
        })
    }

    pub fn own_id(&self) -> SteamId {
        self.client.user().steam_id()
    }

    /// Creates a two-player friends-only lobby that friends can join from Steam.
    pub fn host_lobby(&self) -> Result<u64, String> {
        let (sender, created) = channel();
        self.client
            .matchmaking()
            .create_lobby(LobbyType::FriendsOnly, 2, move |result| {
                let _ = sender.send(result);
            });
        let lobby = created
            .recv_timeout(LOBBY_WINDOW)
            .map_err(|_| "Steam did not create a lobby within 20 seconds".to_owned())?
            .map_err(|error| format!("Steam could not create a lobby: {error}"))?;
        *self.lobby.lock().unwrap() = Some(lobby);
        // Steam's friends list offers Join Game and Invite to Game from this connect string.
        let connect = format!("{CONNECT_LOBBY} {}", lobby.raw());
        self.client
            .friends()
            .set_rich_presence("connect", Some(&connect));
        Ok(lobby.raw())
    }

    /// Opens Steam's invite overlay for the lobby once the overlay has attached to this game's
    /// window, which Steam needs a few seconds for. Returns whether it opened.
    pub fn open_invite_overlay(&self, lobby: u64) -> bool {
        let utils = self.client.utils();
        let until = Instant::now() + OVERLAY_WINDOW;
        while !utils.is_overlay_enabled() {
            if Instant::now() >= until {
                return false;
            }
            std::thread::sleep(Duration::from_millis(100));
        }
        self.client
            .friends()
            .activate_invite_dialog(LobbyId::from_raw(lobby));
        true
    }

    /// Enters a friend's lobby and returns its host.
    pub fn join_lobby(&self, lobby: u64) -> Result<SteamId, String> {
        let (sender, entered) = channel();
        self.client
            .matchmaking()
            .join_lobby(LobbyId::from_raw(lobby), move |result| {
                let _ = sender.send(result);
            });
        let lobby = entered
            .recv_timeout(LOBBY_WINDOW)
            .map_err(|_| "Steam did not enter the lobby within 20 seconds".to_owned())?
            .map_err(|()| "The Steam lobby is full or closed".to_owned())?;
        *self.lobby.lock().unwrap() = Some(lobby);
        Ok(self.client.matchmaking().lobby_owner(lobby))
    }

    /// Lobbies the player chose to join from Steam: an accepted invite, or Join Game.
    pub fn join_requests(&self) -> Receiver<u64> {
        let (sender, requests) = channel();
        let friends = self.client.clone();
        let invite = sender.clone();
        let mut callbacks = self.callbacks.lock().unwrap();
        callbacks.push(
            friends.register_callback(move |request: GameLobbyJoinRequested| {
                let _ = invite.send(request.lobby_steam_id.raw());
            }),
        );
        callbacks.push(
            friends.register_callback(move |request: GameRichPresenceJoinRequested| {
                if let Some(lobby) = connect_lobby(request.connect.split_whitespace()) {
                    let _ = sender.send(lobby);
                }
            }),
        );
        requests
    }

    /// A datagram link over Steam's relay network that accepts sessions only from `allowed`.
    pub fn transport(&self, allowed: impl Fn(SteamId) -> bool + Send + 'static) -> SteamTransport {
        let messages = self.client.networking_messages();
        messages.session_request_callback(move |request| {
            if request.remote().steam_id().is_some_and(&allowed) {
                request.accept();
            } else {
                request.reject();
            }
        });
        SteamTransport {
            messages,
            inbox: Mutex::new(VecDeque::new()),
        }
    }

    /// Whether `user` is a Steam friend or already in this session's lobby.
    pub fn friend_or_member(&self) -> impl Fn(SteamId) -> bool + Send + 'static {
        let client = self.client.clone();
        let lobby = *self.lobby.lock().unwrap();
        move |user| {
            client
                .friends()
                .get_friend(user)
                .has_friend(FriendFlags::IMMEDIATE)
                || lobby
                    .is_some_and(|lobby| client.matchmaking().lobby_members(lobby).contains(&user))
        }
    }
}

impl Drop for Steam {
    fn drop(&mut self) {
        if let Some(lobby) = self.lobby.lock().unwrap().take() {
            self.client.matchmaking().leave_lobby(lobby);
        }
        self.client.friends().clear_rich_presence();
        self.callbacks.lock().unwrap().clear();
        self.pumping.store(false, Ordering::Relaxed);
        if let Some(pump) = self.pump.take() {
            let _ = pump.join();
        }
    }
}

/// The lobby named by Steam's `+connect_lobby <id>` launch option or connect string.
pub fn connect_lobby<'a>(mut words: impl Iterator<Item = &'a str>) -> Option<u64> {
    words.find(|word| *word == CONNECT_LOBBY)?;
    words.next()?.parse().ok()
}

/// Unreliable, unordered messages to Steam users, carried by Steam's relays so no one forwards
/// ports.
pub struct SteamTransport {
    messages: NetworkingMessages,
    inbox: Mutex<VecDeque<(SteamId, Vec<u8>)>>,
}

impl Transport for SteamTransport {
    type Peer = SteamId;
    fn send_to(&self, bytes: &[u8], peer: SteamId) -> io::Result<usize> {
        self.messages
            .send_message_to_user(
                peer.into(),
                SendFlags::UNRELIABLE_NO_NAGLE | SendFlags::AUTO_RESTART_BROKEN_SESSION,
                bytes,
                CHANNEL,
            )
            .map_err(|error| io::Error::other(format!("Steam send: {error}")))?;
        Ok(bytes.len())
    }
    fn recv_from(&self, bytes: &mut [u8]) -> io::Result<(usize, SteamId)> {
        let mut inbox = self.inbox.lock().unwrap();
        let until = Instant::now() + RECEIVE_WAIT;
        while inbox.is_empty() && Instant::now() < until {
            for message in self.messages.receive_messages_on_channel(CHANNEL, 32) {
                if let Some(sender) = message.identity_peer().steam_id() {
                    inbox.push_back((sender, message.data().to_vec()));
                }
            }
            if inbox.is_empty() {
                std::thread::sleep(Duration::from_millis(1));
            }
        }
        let (sender, datagram) = inbox.pop_front().ok_or(io::ErrorKind::TimedOut)?;
        let size = datagram.len().min(bytes.len());
        bytes[..size].copy_from_slice(&datagram[..size]);
        Ok((size, sender))
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn connect_strings_name_the_lobby() {
        assert_eq!(
            connect_lobby("+connect_lobby 109775241077".split_whitespace()),
            Some(109_775_241_077)
        );
        assert_eq!(
            connect_lobby(["quarrel-client", "+connect_lobby", "7"].into_iter()),
            Some(7)
        );
        assert_eq!(connect_lobby("+connect_lobby".split_whitespace()), None);
        assert_eq!(connect_lobby("join 7".split_whitespace()), None);
    }
}
