const express = require('express');
const cors    = require('cors');
const app     = express();

app.use(cors());
app.use(express.json());

const GAME_IP   = process.env.GAME_SERVER_IP   || 'localhost';
const GAME_PORT = parseInt(process.env.GAME_SERVER_PORT || '7777');
const API_PORT  = parseInt(process.env.API_PORT         || '3000');

// ── Storage ───────────────────────────────────────────────────────────────────

let rooms      = {};
let nextRoomId = 1;

// sessionId → lastSeen (ms). Clients ping every 60 s; removed after 3 min silence.
const playerSessions = new Map();

// ── Routes ────────────────────────────────────────────────────────────────────

// GET /status — used by launcher / clients to verify API is reachable
app.get('/status', (req, res) => {
    const all     = Object.values(rooms);
    const waiting = all.filter(r => r.status === 'waiting');
    res.json({
        ok:              true,
        gameServerIP:    GAME_IP,
        gameServerPort:  GAME_PORT,
        waitingRooms:    waiting.length,
        totalRooms:      all.length,
        online:          playerSessions.size,
    });
});

// POST /players/ping — heartbeat; registers the client as online
app.post('/players/ping', (req, res) => {
    const { sessionId } = req.body;
    if (sessionId) playerSessions.set(sessionId, Date.now());
    res.json({ online: playerSessions.size });
});

// GET /rooms — list rooms waiting for a second player
app.get('/rooms', (req, res) => {
    const waiting = Object.values(rooms).filter(r => r.status === 'waiting');
    res.json(waiting);
});

// POST /rooms — create a room (host registers that they are waiting)
app.post('/rooms', (req, res) => {
    const { playerName, sessionToken } = req.body;
    const id = nextRoomId++;

    rooms[id] = {
        id,
        playerName:     playerName   || 'Joueur',
        sessionToken:   sessionToken || '',
        status:         'waiting',
        gameServerIP:   GAME_IP,
        gameServerPort: GAME_PORT,
        createdAt:      Date.now(),
    };

    console.log(`[${ts()}] Room #${id} créée par "${rooms[id].playerName}"`);
    res.status(201).json(rooms[id]);
});

// POST /rooms/:id/join — second player joins, marks room as starting
app.post('/rooms/:id/join', (req, res) => {
    const room = rooms[req.params.id];
    if (!room)                        return res.status(404).json({ error: 'Room introuvable' });
    if (room.status !== 'waiting')    return res.status(409).json({ error: 'Room non disponible' });

    const { sessionToken } = req.body;
    // Empêche un joueur de rejoindre sa propre salle (token identique)
    if (sessionToken && room.sessionToken && sessionToken === room.sessionToken)
        return res.status(409).json({ error: 'Impossible de rejoindre sa propre salle' });

    room.status = 'starting';
    console.log(`[${ts()}] Room #${room.id} rejointe — partie en cours de lancement`);
    res.json(room);

    // Room cleanup after 60 s — by then the game server has the players
    setTimeout(() => { delete rooms[room.id]; }, 60_000);
});

// DELETE /rooms/:id — explicit cleanup (e.g. host cancelled before second player joined)
app.delete('/rooms/:id', (req, res) => {
    if (rooms[req.params.id]) {
        console.log(`[${ts()}] Room #${req.params.id} supprimée`);
        delete rooms[req.params.id];
    }
    res.json({ ok: true });
});

// ── Cleanup stale rooms (waiting > 10 min) ────────────────────────────────────

setInterval(() => {
    const cutoff = Date.now() - 10 * 60_000;
    for (const [id, room] of Object.entries(rooms)) {
        if (room.createdAt < cutoff) {
            console.log(`[${ts()}] Room #${id} expirée — suppression`);
            delete rooms[id];
        }
    }
}, 60_000);

// ── Cleanup stale player sessions (silent > 3 min) ────────────────────────────

setInterval(() => {
    const cutoff = Date.now() - 3 * 60_000;
    for (const [id, lastSeen] of playerSessions) {
        if (lastSeen < cutoff) playerSessions.delete(id);
    }
}, 60_000);

// ── Start ─────────────────────────────────────────────────────────────────────

app.listen(API_PORT, () => {
    console.log(`[${ts()}] Matchmaking API démarrée sur le port ${API_PORT}`);
    console.log(`[${ts()}] Serveur de jeu : ${GAME_IP}:${GAME_PORT}`);
});

function ts() {
    return new Date().toISOString().slice(11, 19);
}
