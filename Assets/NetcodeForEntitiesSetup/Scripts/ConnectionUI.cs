using System;
using System.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM 
using UnityEngine.InputSystem.UI;
#endif
namespace Unity.Multiplayer.Center.NetcodeForEntitiesSetup
{
    /// <summary>
    /// A basic example of a UI to start a host or client.
    /// If you want to modify this Script please copy it into your own project and add it to your copied UI Prefab.
    /// </summary>
    public class ConnectionUI : MonoBehaviour
    {
        /// <summary>
        /// The network port to use.
        /// </summary>
        public ushort NetworkPort = 7979;

        /// <summary>
        /// The Ipv4 address to use.
        /// </summary>
        public string Address = "127.0.0.1";

        /// <summary>
        /// The name of the scene to load (without extension).
        /// </summary>
        public string SceneToLoad;
        
        public Text ConnectionLabel;
        
        public Button StartHostButton;
        
        public Button StartClientButton;
        public Button RetryButton;
        public Button DisconnectButton;
        [Tooltip("Choose a local profile before connecting. Different local players need different profiles.")]
        public string LocalProfile = "";
        [Tooltip("Choose a player explicitly if this profile has several saved tickets. -1 selects its only ticket.")]
        public int ResumePlayerId = -1;

        /// <summary>
        /// Stores the old name of the local world (create by initial bootstrap).
        /// It is reused later when the local world is created when coming back from game to the menu.
        /// </summary>
        internal static string OldFrontendWorldName = string.Empty;
        bool loadingGameScene;
        bool retryRequested, freshRequested, hosted;
        Text sessionStatus;
        Canvas sessionCanvas;
        Button freshButton;
        string joinFailure;
        World ownedClient;
        World hostedServer;
        InputField profileInput, playerInput;

        /// <summary>
        /// Gets or sets the Connection status text in the UI.
        /// </summary>
        public string ConnectionStatus
        {
            get => ConnectionLabel ? ConnectionLabel.text : string.Empty;
            set
            {
                if (ConnectionLabel) ConnectionLabel.text = value;
                if (sessionStatus) sessionStatus.text = value;
            }
        }

        /// <summary>
        /// Called before a connection is established.
        /// </summary>
        public void OnBeforeConnect()
        {
            // Prevent disconnection on focus loss.
            Application.runInBackground = true;
        }
        
        /// <summary>
        /// Called when the connection is established.
        /// </summary>
        public void OnConnected()
        {
            if (loadingGameScene) return;
            loadingGameScene = true;
            DestroyLocalSimulationWorld();
            // Disposing the old local World clears DefaultGameObjectInjectionWorld.
            // Presentation scripts must subsequently use the new client World.
            if (ownedClient != null && ownedClient.IsCreated) World.DefaultGameObjectInjectionWorld = ownedClient;
            StartCoroutine(ShowGameScene());
        }

        IEnumerator ShowGameScene()
        {
            var frontend = gameObject.scene;
            var scene = SceneManager.GetSceneByName(SceneToLoad);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                var load = SceneManager.LoadSceneAsync(SceneToLoad, LoadSceneMode.Additive);
                if (load != null) yield return load;
                scene = SceneManager.GetSceneByName(SceneToLoad);
            }
            if (scene.IsValid() && scene.isLoaded)
            {
                SceneManager.SetActiveScene(scene);
                // Retain the network UI controller/status references, but stop the
                // frontend from covering the game or competing for Camera.main/input.
                if (frontend != scene)
                    foreach (var root in frontend.GetRootGameObjects())
                    {
                        foreach (var camera in root.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
                        foreach (var listener in root.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
                        foreach (var canvas in root.GetComponentsInChildren<Canvas>(true)) if (canvas != sessionCanvas) canvas.enabled = false;
                        foreach (var events in root.GetComponentsInChildren<EventSystem>(true))
                            if (!sessionCanvas || !events.transform.IsChildOf(sessionCanvas.transform)) events.enabled = false;
                        foreach (var light in root.GetComponentsInChildren<Light>(true)) light.enabled = false;
                    }
            }
            loadingGameScene = false;
            EnsureSessionControls();
        }

        void Awake()
        {   
            if (StartHostButton) StartHostButton.onClick.AddListener(StartClientServer);
            if (StartClientButton) StartClientButton.onClick.AddListener(StartClient);
            
            if (!FindAnyObjectByType<EventSystem>())
            {
                var inputType = typeof(StandaloneInputModule);
#if ENABLE_INPUT_SYSTEM
                inputType = typeof(InputSystemUIInputModule);                
#endif
                var eventSystem = new GameObject("EventSystem", typeof(EventSystem), inputType);
                eventSystem.transform.SetParent(transform);
            }
            EnsureSessionControls();
            ShowStatus(false, "Choose a local profile, then join or rejoin.");
        }

        void Update()
        {
            if (!retryRequested) return;
            retryRequested = false;
            ConnectClient(freshRequested, !freshRequested); freshRequested = false;
        }

        public void RetryConnection() { freshRequested = false; retryRequested = true; }
        public void StartFreshClient()
        {
            ResumePlayerId = -1;
            if (playerInput) playerInput.SetTextWithoutNotify("");
            freshRequested = true; retryRequested = true;
        }

        internal void NotePlayer(int playerId)
        {
            ResumePlayerId = playerId;
            if (playerInput) playerInput.SetTextWithoutNotify(playerId.ToString());
        }

        public void Disconnect()
        {
            if (ownedClient != null && ownedClient.IsCreated)
            {
                var world = ownedClient;
                using var query = world.EntityManager.CreateEntityQuery(typeof(NetworkStreamConnection));
                using var connections = query.ToEntityArray(Unity.Collections.Allocator.Temp);
                foreach (var connection in connections)
                    if (!world.EntityManager.HasComponent<NetworkStreamRequestDisconnect>(connection))
                        world.EntityManager.AddComponent<NetworkStreamRequestDisconnect>(connection);
            }
        }

        internal void NoteFailure(PlayerJoinRejection reason)
        {
            joinFailure = reason == PlayerJoinRejection.NoAvailableSlot ? "No unused player slot is available."
                : reason == PlayerJoinRejection.WrongMatch ? "The saved ticket belongs to another match."
                : reason == PlayerJoinRejection.ProfileRequired ? "Choose a local profile before connecting."
                : reason == PlayerJoinRejection.AmbiguousLocalProfile ? "Several saved players use this profile; enter the player ID."
                : reason == PlayerJoinRejection.TicketUnavailable ? "The local resume ticket could not be read."
                : reason == PlayerJoinRejection.LocalPrefabsTimedOut ? "Local scene/catalog/ghost prefabs did not finish loading within 60 seconds."
                : "The server rejected the resume ticket.";
        }

        internal void ShowStatus(bool connected, string status)
        {
            string clientStatus = !connected && !string.IsNullOrEmpty(joinFailure) ? joinFailure
                : connected && !string.IsNullOrEmpty(PlayerResumeStore.LastError) ? status + " | " + PlayerResumeStore.LastError : status;
            ConnectionStatus = (hosted ? "Server: " + (hostedServer != null && hostedServer.IsCreated ? "Running" : "Stopped") + "\n" : "")
                + "Client: " + clientStatus;
            if (RetryButton) RetryButton.interactable = !connected;
            if (DisconnectButton) DisconnectButton.interactable = connected;
            if (freshButton) freshButton.interactable = !connected;
            if (profileInput) profileInput.interactable = !connected;
            if (playerInput) playerInput.interactable = !connected;
            if (StartClientButton) StartClientButton.interactable = !connected;
        }

        void EnsureSessionControls()
        {
            if (sessionCanvas) { EnsureSessionEvents(); return; }
            var root = new GameObject("Session connection controls", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            // An independent root canvas survives hiding the frontend canvas.
            SceneManager.MoveGameObjectToScene(root, gameObject.scene);
            sessionCanvas = root.GetComponent<Canvas>(); sessionCanvas.renderMode = RenderMode.ScreenSpaceOverlay; sessionCanvas.sortingOrder = 200;
            var panel = new GameObject("Controls", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(root.transform, false);
            var rect = panel.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, 1);
            rect.anchoredPosition = new Vector2(-12, -64); rect.sizeDelta = new Vector2(340, 122);
            panel.GetComponent<Image>().color = new Color(0.05f, 0.06f, 0.08f, 0.85f);
            sessionStatus = AddLabel(panel.transform, "Status", new Vector2(10, -8), new Vector2(320, 38));
            sessionStatus.text = ConnectionStatus;
            profileInput = AddInput(panel.transform, "Local profile", 10, 195, LocalProfile);
            profileInput.characterLimit = 32;
            profileInput.onValueChanged.AddListener(value => LocalProfile = value.Trim());
            playerInput = AddInput(panel.transform, "Player ID (auto)", 215, 115, ResumePlayerId >= 0 ? ResumePlayerId.ToString() : "");
            playerInput.contentType = InputField.ContentType.IntegerNumber;
            playerInput.onValueChanged.AddListener(value => ResumePlayerId = int.TryParse(value, out var id) && id >= 0 ? id : -1);
            if (!RetryButton) RetryButton = AddButton(panel.transform, "Rejoin", 10, RetryConnection);
            if (!DisconnectButton) DisconnectButton = AddButton(panel.transform, "Disconnect", 120, Disconnect);
            freshButton = AddButton(panel.transform, "New player", 230, StartFreshClient);
            EnsureSessionEvents();
        }

        void EnsureSessionEvents()
        {
            var systems = FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            foreach (var system in systems)
                if (system.isActiveAndEnabled && !system.transform.IsChildOf(sessionCanvas.transform))
                {
                    // Prefer the game's existing event system after the frontend is hidden.
                    foreach (var own in sessionCanvas.GetComponentsInChildren<EventSystem>(true)) own.enabled = false;
                    return;
                }
            var ownedEvents = sessionCanvas.GetComponentInChildren<EventSystem>(true);
            if (ownedEvents) { ownedEvents.enabled = true; return; }
            {
                var input = typeof(StandaloneInputModule);
#if ENABLE_INPUT_SYSTEM
                input = typeof(InputSystemUIInputModule);
#endif
                new GameObject("Session EventSystem", typeof(EventSystem), input).transform.SetParent(sessionCanvas.transform, false);
            }
        }

        InputField AddInput(Transform parent, string placeholder, float x, float width, string value)
        {
            var item = new GameObject(placeholder, typeof(RectTransform), typeof(Image), typeof(InputField)); item.transform.SetParent(parent, false);
            var rect = item.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -48); rect.sizeDelta = new Vector2(width, 26);
            item.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.18f);
            var text = AddLabel(item.transform, "Value", new Vector2(5, -4), new Vector2(width - 10, 20));
            var hint = AddLabel(item.transform, "Placeholder", new Vector2(5, -4), new Vector2(width - 10, 20));
            hint.text = placeholder; hint.color = Color.gray;
            var input = item.GetComponent<InputField>(); input.textComponent = text; input.placeholder = hint; input.text = value;
            return input;
        }

        Text AddLabel(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var label = new GameObject(name, typeof(RectTransform), typeof(Text)); label.transform.SetParent(parent, false);
            var rect = label.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position; rect.sizeDelta = size;
            var text = label.GetComponent<Text>(); text.font = ConnectionLabel && ConnectionLabel.font ? ConnectionLabel.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 13; text.color = Color.white; text.raycastTarget = false;
            return text;
        }

        Button AddButton(Transform parent, string title, float x, UnityEngine.Events.UnityAction action)
        {
            var item = new GameObject(title, typeof(RectTransform), typeof(Image), typeof(Button)); item.transform.SetParent(parent, false);
            var rect = item.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -84); rect.sizeDelta = new Vector2(100, 28);
            item.GetComponent<Image>().color = new Color(0.2f, 0.23f, 0.28f);
            var label = AddLabel(item.transform, "Label", Vector2.zero, rect.sizeDelta); label.text = title; label.alignment = TextAnchor.MiddleCenter;
            var button = item.GetComponent<Button>(); button.onClick.AddListener(action); return button;
        }

        void AddConnectionUISystemToUpdateList()
        {
            var world = ownedClient;
            if (world != null && world.IsCreated && !world.IsThinClient())
            {
                var sys = world.GetOrCreateSystemManaged<ConnectionUISystem>();
                sys.UIBehaviour = this;
                var simGroup = world.GetExistingSystemManaged<SimulationSystemGroup>();
                simGroup.AddSystemToUpdateList(sys);
            }
        }
        
        void StartClientServer()
        {
            if (!ValidateProfile()) return;
            if (ClientServerBootstrap.RequestedPlayType != ClientServerBootstrap.PlayType.ClientAndServer)
            {
                Debug.LogError($"Creating client/server worlds is not allowed if playmode is set to {ClientServerBootstrap.RequestedPlayType}");
                return;
            }

            OnBeforeConnect();
            DisableButtons();
            hosted = true; joinFailure = null;
           
            var server = ClientServerBootstrap.CreateServerWorld("ServerWorld");
            hostedServer = server;
            var client = ClientServerBootstrap.CreateClientWorld("ClientWorld");
            ownedClient = client;
            
            if (World.DefaultGameObjectInjectionWorld == null)
                World.DefaultGameObjectInjectionWorld = server;

            OnConnected();
            
            NetworkEndpoint ep = NetworkEndpoint.AnyIpv4.WithPort(NetworkPort);
            {
                using var drvQuery = server.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<NetworkStreamDriver>());
                drvQuery.GetSingletonRW<NetworkStreamDriver>().ValueRW.Listen(ep);
            }

            ep = NetworkEndpoint.LoopbackIpv4.WithPort(NetworkPort);
            PlayerResumeStore.Configure(client, ep, true, LocalProfile);
            {
                using var drvQuery = client.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<NetworkStreamDriver>());
                drvQuery.GetSingletonRW<NetworkStreamDriver>().ValueRW.Connect(client.EntityManager, ep);
            }
            AddConnectionUISystemToUpdateList();
        }
        
        void StartClient()
        {
            ConnectClient(false);
        }

        void ConnectClient(bool fresh, bool resumeOnly = false)
        {
            if (!ValidateProfile()) return;
            var ep = hosted ? NetworkEndpoint.LoopbackIpv4.WithPort(NetworkPort) : NetworkEndpoint.Parse(Address, NetworkPort);
            if (resumeOnly && PlayerResumeStore.Lookup(ep.ToString(), LocalProfile, ResumePlayerId, out _) != ResumeLookup.Found)
            {
                joinFailure = "No unique saved player for this profile. Start Host/Client first, or select the saved player ID.";
                ShowStatus(false, joinFailure);
                return;
            }
            OnBeforeConnect();
            DisableButtons(); joinFailure = null;
            // A new client World clears stale snapshots, loading state and local command queues.
            // The host's authoritative server World continues running throughout the retry.
            var client = ReplaceOwnedClientWorld();
            
            if (World.DefaultGameObjectInjectionWorld == null)
                World.DefaultGameObjectInjectionWorld = client;
            
            OnConnected();

            PlayerResumeStore.Configure(client, ep, fresh, LocalProfile, ResumePlayerId);
            {
                using var drvQuery = client.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<NetworkStreamDriver>());
                drvQuery.GetSingletonRW<NetworkStreamDriver>().ValueRW.Connect(client.EntityManager, ep);
            }
            AddConnectionUISystemToUpdateList();
        }

        World ReplaceOwnedClientWorld()
        {
            if (ownedClient != null && ownedClient.IsCreated)
            {
                if (!ownedClient.IsClient() || ownedClient.IsServer())
                    throw new InvalidOperationException("Rejoin can only replace the owned client World.");
                ownedClient.Dispose();
            }
            ownedClient = ClientServerBootstrap.CreateClientWorld("ClientWorld");
            World.DefaultGameObjectInjectionWorld = ownedClient;
            return ownedClient;
        }

        bool ValidateProfile()
        {
            LocalProfile = (LocalProfile ?? string.Empty).Trim();
            if (LocalProfile.Length > 0 && LocalProfile.Length <= 32 && System.Text.Encoding.UTF8.GetByteCount(LocalProfile) <= 125) return true;
            joinFailure = "Enter a local profile (up to 32 characters) before connecting.";
            ShowStatus(false, joinFailure); return false;
        }

        void OnDestroy()
        {
            if (sessionCanvas) Destroy(sessionCanvas.gameObject);
        }

        static void DestroyLocalSimulationWorld()
        {
            foreach (var world in World.All)
            {
                if (world.Flags == WorldFlags.Game)
                {
                    OldFrontendWorldName = world.Name;
                    world.Dispose();
                    break;
                }
            }
        }

        void DisableButtons()
        {
            if (StartHostButton) StartHostButton.interactable = false;
            if (StartClientButton) StartClientButton.interactable = false;
        }
    }
    
    /// <summary>
    /// System making the link between the UI and the network connection status.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    [DisableAutoCreation]
    public partial class ConnectionUISystem : SystemBase
    {
        /// <summary>
        /// The UI behaviour to update.
        /// </summary>
        public ConnectionUI UIBehaviour;
        string m_PingText;

        /// <summary>
        /// Updates the UI with the connection status.
        /// </summary>
        protected override void OnUpdate()
        {
            CompleteDependency();
            if (!UIBehaviour) return;
            if (!SystemAPI.TryGetSingletonEntity<NetworkStreamConnection>(out var connectionEntity))
            {
                UIBehaviour.ShowStatus(false, "Disconnected | Rejoin resumes your saved player.");
                m_PingText = default;
                return;
            }

            var connection = EntityManager.GetComponentData<NetworkStreamConnection>(connectionEntity);
            if (EntityManager.HasComponent<ClientJoinFailure>(connectionEntity)) UIBehaviour.NoteFailure(EntityManager.GetComponentData<ClientJoinFailure>(connectionEntity).Reason);
            if (EntityManager.HasComponent<NetworkStreamRequestDisconnect>(connectionEntity))
            { UIBehaviour.ShowStatus(false, "Disconnected | Rejoin resumes your saved player."); return; }
            var address = SystemAPI.GetSingletonRW<NetworkStreamDriver>().ValueRO.GetRemoteEndPoint(connection).Address;
            if (EntityManager.HasComponent<NetworkId>(connectionEntity))
            {
                if (string.IsNullOrEmpty(m_PingText) || UnityEngine.Time.frameCount % 30 == 0)
                {
                    var networkSnapshotAck = EntityManager.GetComponentData<NetworkSnapshotAck>(connectionEntity);
                    m_PingText = networkSnapshotAck.EstimatedRTT > 0 ? $"{(int) networkSnapshotAck.EstimatedRTT}ms" : "Connected";
                }

                bool ready = EntityManager.HasComponent<ClientPlayerState>(connectionEntity)
                    && EntityManager.GetComponentData<ClientPlayerState>(connectionEntity).Phase == ClientLoadingPhase.ServerReady;
                int playerId = EntityManager.HasComponent<ClientPlayerState>(connectionEntity) ? EntityManager.GetComponentData<ClientPlayerState>(connectionEntity).PlayerId : -1;
                if (playerId >= 0) UIBehaviour.NotePlayer(playerId);
                UIBehaviour.ShowStatus(true, ready ? $"Player {playerId} | {address} | {m_PingText}" : $"{address} | Receiving current match...");
            }
            else
            {
                UIBehaviour.ShowStatus(true, $"{address} | Connecting");
            }
        }
    }
}
