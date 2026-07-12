using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Astraleum.Core;
using Microsoft.AspNetCore.SignalR.Client;
using UnityEngine;

namespace Astraleum
{
    /// <summary>
    /// Wrapper SignalR persistant (DontDestroyOnLoad).
    /// Gère la connexion au hub ASP.NET et dispatch les événements sur le thread principal Unity.
    /// </summary>
    public class SignalRGameClient : MonoBehaviour
    {
        public static SignalRGameClient Instance { get; private set; }

        private HubConnection _connection;
        private readonly ConcurrentQueue<System.Action> _mainThreadQueue = new ConcurrentQueue<System.Action>();

        // ── Derniers messages reçus (buffer pour abonnements tardifs après changement de scène) ──

        public GameSetupMessage  LastSetup    { get; private set; }
        public GameStateSnapshot LastSnapshot { get; private set; }

        public bool IsConnected => _connection?.State == HubConnectionState.Connected;

        // ── Événements (toujours déclenchés sur le thread principal) ─────────────────────────────

        public event System.Action<GameSetupMessage>   OnGameSetup;
        public event System.Action<GameStateSnapshot>  OnStateUpdate;
        public event System.Action                     OnGameCancelled;
        public event System.Action<string>             OnActionError;
        public event System.Action<ArrowUpdateAction>  OnArrowUpdate;
        public event System.Action<SkillExecutedEvent> OnSkillExecuted;
        public event System.Action<IncantationResolvedEvent> OnIncantationResolved;
        public event System.Action<List<CombatLogEntry>> OnCombatLog;

        // ── Lifecycle Unity ────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Application.runInBackground = true;
        }

        private void Update()
        {
            while (_mainThreadQueue.TryDequeue(out var action))
                action?.Invoke();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            _ = DisconnectAsync();
        }

        // ── Connexion ──────────────────────────────────────────────────────────────────────────

        public async Task ConnectAsync(string hubUrl)
        {
            if (_connection != null)
                await _connection.DisposeAsync();

            _connection = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .WithAutomaticReconnect()
                .Build();

            _connection.On<GameSetupMessage>("GameSetup", msg =>
            {
                LastSetup = msg;
                _mainThreadQueue.Enqueue(() => OnGameSetup?.Invoke(msg));
            });

            _connection.On<GameStateSnapshot>("StateUpdate", snap =>
            {
                LastSnapshot = snap;
                _mainThreadQueue.Enqueue(() => OnStateUpdate?.Invoke(snap));
            });

            _connection.On("GameCancelled", () =>
                _mainThreadQueue.Enqueue(() => OnGameCancelled?.Invoke()));

            _connection.On<string>("ActionError", err =>
                _mainThreadQueue.Enqueue(() => OnActionError?.Invoke(err)));

            _connection.On<ArrowUpdateAction>("ArrowUpdate", action =>
                _mainThreadQueue.Enqueue(() => OnArrowUpdate?.Invoke(action)));

            _connection.On<SkillExecutedEvent>("SkillExecuted", evt =>
                _mainThreadQueue.Enqueue(() => OnSkillExecuted?.Invoke(evt)));

            _connection.On<IncantationResolvedEvent>("IncantationResolved", evt =>
                _mainThreadQueue.Enqueue(() => OnIncantationResolved?.Invoke(evt)));

            _connection.On<List<CombatLogEntry>>("CombatLog", entries =>
                _mainThreadQueue.Enqueue(() => OnCombatLog?.Invoke(entries)));

            await _connection.StartAsync();
            Debug.Log($"[SignalR] Connecté à {hubUrl}");
        }

        public async Task DisconnectAsync()
        {
            if (_connection != null)
            {
                try { await _connection.StopAsync(); } catch { }
                await _connection.DisposeAsync();
                _connection = null;
            }
            LastSetup    = null;
            LastSnapshot = null;
        }

        // ── Invocations Client → Serveur ────────────────────────────────────────────────────────

        public Task JoinGame(string roomId, string playerName, List<int> deck)
            => _connection?.InvokeAsync("JoinGame", roomId, playerName, deck) ?? Task.CompletedTask;

        public Task ExecuteSkill(ExecuteSkillAction action)
            => _connection?.InvokeAsync("ExecuteSkill", action) ?? Task.CompletedTask;

        public Task EndTurn()
            => _connection?.InvokeAsync("EndTurn") ?? Task.CompletedTask;

        public Task GiveUp()
            => _connection?.InvokeAsync("GiveUp") ?? Task.CompletedTask;

        public Task CancelGame()
            => _connection?.InvokeAsync("CancelGame") ?? Task.CompletedTask;

        public Task UpdateArrow(ArrowUpdateAction action)
            => _connection?.InvokeAsync("UpdateArrow", action) ?? Task.CompletedTask;
    }
}
