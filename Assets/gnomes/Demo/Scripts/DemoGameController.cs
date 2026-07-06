using System.Collections.Generic;
using System.Linq;
using GNOMES.Actor.Core;
using GNOMES.Actor.Core.Scene;
using GNOMES.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class DemoGameController : MonoBehaviour {
    [Header("Prefabs & References")]
    [SerializeField] private GameObject PlayerActorPrefab;
    [SerializeField] private PlayerInputManager InputManager;
    [SerializeField] private PlayerBrain DefaultPlayerBrain;

    // Track active players to decouple from single-player SceneManager references
    private readonly List<Actor> _activePlayers = new List<Actor>();
    private int _playerNum;

    private void OnEnable() {
        InputManager.onPlayerJoined += HandlePlayerJoined;
        
        if (!System.Diagnostics.Debugger.IsAttached) {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void OnDisable() {
        if (InputManager != null) {
            InputManager.onPlayerJoined -= HandlePlayerJoined;
        }
    }

    private void HandlePlayerJoined(PlayerInput playerInput) {
        if (PlayerActorPrefab == null) {
            Debug.LogError("[Gnomes] DemoGameController: Player Actor Prefab is not set.");
            return;
        }
        
        var player = playerInput.gameObject.GetComponent<Player>();
        int playerIndex = _activePlayers.Count; 

        if (player == null) {
            Debug.LogError("[Gnomes] DemoGameController: Joined PlayerInput object missing Player component.");
            return;
        } else if (GnomesSceneManager.HasPlayer(player) || GnomesSceneManager.HasPlayer(playerIndex)) {
            Debug.LogWarning($"[Gnomes] {player} has already been initiated");
            return;
        }

        // 1. Query spatial data from the scene system instead of blind spawning
        // Fallback to finding a specific spawn point or default
        var spawnPoint = GnomesSpawnPoint.Find("Default", playerIndex);
        
        Vector3 spawnPos = spawnPoint != null ? spawnPoint.transform.position : Vector3.zero;
        Quaternion spawnRot = spawnPoint != null ? spawnPoint.transform.rotation : Quaternion.identity;

        // 2. Allocate the Actor via your DDOL Pooler
        var actorGo = Pooler.Spawn(PlayerActorPrefab);
        actorGo.transform.position = spawnPos;
        actorGo.transform.rotation = spawnRot;
        var spawnedActor = actorGo.GetComponent<Actor>();
        
        if (spawnedActor == null) {
            Debug.LogError("[Gnomes] DemoGameController: Pooler prefab missing Actor component.");
            return;
        }

        // 3. Name and track the player locally
        actorGo.name = $"Player_Actor#{_playerNum}";
        _activePlayers.Add(spawnedActor);

        // 4. Bind the player brain architecture cleanly
        var playerBrain = Instantiate(DefaultPlayerBrain);
        playerBrain.Player = player; 
        spawnedActor.SwapBrain(playerBrain);

        // 5. Notify the Scene Manager so it can track this player for future transitions
        GnomesSceneManager.RegisterPlayer(_playerNum,player,DefaultPlayerBrain,PlayerActorPrefab);
        GnomesSceneManager.AssignActorToSlot(player,spawnedActor);
        Debug.Log($"[Gnomes] Decoupled player setup complete for {actorGo.name} at {spawnPos}.");
        _playerNum++;
    }
}