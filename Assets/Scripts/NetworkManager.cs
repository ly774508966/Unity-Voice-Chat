﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(NetworkView))]
public class NetworkManager : Singleton<NetworkManager>
{
	private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
	
	private string gameTypeName = "Sample_LockStep_Network";
	private bool refreshing = false;
	private HostData[] hostData;
	//TODO: Add ability to allow hosting user to set this number
	public int NumberOfPlayers = 2;
	
	public Dictionary<string, NetworkPlayer> players;
	
	public delegate void NetworkManagerEvent();
	public static event NetworkManagerEvent OnConnectedToGame;
	public static event NetworkManagerEvent OnGameStart;
	public static bool gameStarted = false;
	
	private NetworkView nv;
	
	//GUI Variables
	private string gameName = "Default Game Name";
	private Vector2 scrollPosition;
	
	private void Start()
	{
		players = new Dictionary<string, NetworkPlayer>(NumberOfPlayers);
		nv = GetComponent<NetworkView>();
		nv.stateSynchronization = NetworkStateSynchronization.Off;
		scrollPosition = new Vector2();
		
		refreshHostList();
	}
	
	private void Update()
	{
		if (refreshing)
		{
			if (MasterServer.PollHostList().Length > 0)
			{
				refreshing = false;
				log.Debug("HostList Length: " + MasterServer.PollHostList().Length);
				hostData = MasterServer.PollHostList();
			}
		}
	}
	
	private void startServer()
	{
		log.Debug("startServer called");
		
		bool useNAT = !Network.HavePublicAddress();
		Network.InitializeServer(32, 25001, useNAT);
		MasterServer.RegisterHost(gameTypeName, gameName, NetworkHostMessages.GenerateHostComment(NumberOfPlayers));
		players.Add(Network.player.ToString(), Network.player);
		Player.Instance.PlayerNumber = 0;
	}
	
	private void refreshHostList()
	{
		MasterServer.RequestHostList(gameTypeName);
		refreshing = true;
	}
	
	private void PrintHostData()
	{
		HostData[] hostData = MasterServer.PollHostList();
		log.Debug("HostData length " + hostData.Length);
	}
	
	#region Messages
	private void OnServerInitialized()
	{
		log.Debug("Server initialized");
		log.Debug("Expected player count : " + NumberOfPlayers);
		//Notify any delegates that we are connected to the game
		if (OnConnectedToGame != null)
		{
			OnConnectedToGame();
		}
	}
	
	private void OnMasterServerEvent(MasterServerEvent mse)
	{
		log.Debug("Master Server Event: " + mse.ToString());
	}
	
	private void OnPlayerConnected(NetworkPlayer player)
	{
		players.Add(player.ToString(), player);
		log.Debug("OnPlayerConnected, playerID:" + player.ToString());
		log.Debug("Player Count : " + players.Count);
		//Once all expected players have joined, send all clients the list of players
		if (players.Count == NumberOfPlayers)
		{
			foreach (NetworkPlayer p in players.Values)
			{
				log.Debug("Calling RegisterPlayerAll...");
				nv.RPC("RegisterPlayerAll", RPCMode.Others, p);
			}
			
			//start the game
			nv.RPC("StartGame", RPCMode.All);
		}
	}
	
	/// <summary>
	/// Called on clients only. Passes all connected players to be added to the players dictionary.
	/// </summary>
	[RPC]
	public void RegisterPlayerAll(NetworkPlayer player)
	{
		log.Debug("Register Player All called for " + player.ToString());
		players.Add(player.ToString(), player);
	}
	
	[RPC]
	public void StartGame()
	{
		//send the start of game event
		if (OnGameStart != null)
		{
			OnGameStart();
			gameStarted = true;
		}
	}
	
	void OnDisconnectedFromServer(NetworkDisconnection info)
	{
		if (Network.isServer)
			log.Debug("Local server connection disconnected");
		else
			if (info == NetworkDisconnection.LostConnection)
				log.Debug("Lost connection to the server");
		else
			log.Debug("Successfully diconnected from the server");
	}
	#endregion
	
	#region GUI
	private void OnGUI()
	{
		if (!gameStarted)
		{
			//Draw the background to the menu
			GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Util.whiteSquare);
		}
		
		if (!Network.isClient && !Network.isServer)
		{
			
			//Setup the GUIStyles
			GUI.skin.button.normal.textColor = Color.white;
			GUI.skin.button.fontSize = (int)(15 * Util.GetScale());
			GUI.skin.textField.normal.textColor = Color.white;
			GUI.skin.textField.fontSize = (int)(12 * Util.GetScale());
			GUI.skin.label.normal.textColor = Color.black;
			GUI.skin.label.fontSize = (int)(15 * Util.GetScale());
			
			DrawTitle();
			
			GUILayout.BeginArea(new Rect(0f, Screen.height * 0.33f, Screen.width, Screen.height * 0.6f));
			GUILayout.BeginHorizontal();
			GUILayout.Space(Screen.width * 0.265f);
			
			//First vertical contains menu buttons
			GUILayout.BeginVertical();
			if (GUILayout.Button("Start Server",
			                     GUILayout.Width(125f * Util.GetScale()),
			                     GUILayout.Height(50f * Util.GetScale())))
			{
				log.Debug("Starting Server");
				startServer();
			}
			
			GUILayout.Space(10f * Util.GetScale());
			
			if (GUILayout.Button("Refresh Hosts",
			                     GUILayout.Width(125f * Util.GetScale()),
			                     GUILayout.Height(50f * Util.GetScale())))
			{
				log.Debug("Refreshing Hosts");
				refreshHostList();
			}
			GUILayout.FlexibleSpace();
			GUILayout.EndVertical();
			
			GUILayout.Space(10f * Util.GetScale());
			
			//Second vertical contains host data
			GUILayout.BeginVertical(GUILayout.Width(225f));
			GUILayout.Label("Server Name:",
			                GUILayout.Width(200f * Util.GetScale()),
			                GUILayout.Height(25f * Util.GetScale()));
			gameName = GUILayout.TextField(gameName,
			                               GUILayout.Width(200f * Util.GetScale()),
			                               GUILayout.Height(25f * Util.GetScale()));
			GUILayout.Space(10f * Util.GetScale());
			GUILayout.Label("Select a Game to Join:",
			                GUILayout.Width(200f * Util.GetScale()),
			                GUILayout.Height(25f * Util.GetScale()));
			
			scrollPosition = GUILayout.BeginScrollView(scrollPosition);
			if (hostData != null)
			{
				foreach (HostData hd in hostData)
				{
					if (GUILayout.Button(hd.gameName,
					                     GUILayout.Width(200f * Util.GetScale()),
					                     GUILayout.Height(25f * Util.GetScale())))
					{
						log.Debug("Connecting to server");
						Network.Connect(hd);
						//Notify any delegates that we are connected to the game
						if (OnConnectedToGame != null)
						{
							OnConnectedToGame();
						}
					}
					GUILayout.Space(10f * Util.GetScale());
				}
			}
			GUILayout.FlexibleSpace();
			GUILayout.EndScrollView();
			GUILayout.EndVertical();
			
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
			GUILayout.EndArea();
		}
		else if (!gameStarted)
		{
			GUIStyle textStyle = new GUIStyle();
			textStyle.normal.textColor = Color.black;
			textStyle.fontSize = (int)(25 * Util.GetScale());
			
			DrawTitle();
			
			GUILayout.BeginArea(new Rect(0f, 0f, Screen.width, Screen.height));
			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			GUILayout.BeginVertical();
			GUILayout.FlexibleSpace();
			GUILayout.Label("Waiting for another user to connect...", textStyle);
			GUILayout.FlexibleSpace();
			GUILayout.EndVertical();
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
			GUILayout.EndArea();
		}
	}
	
	private void DrawTitle()
	{
		GUIStyle titleStyle = new GUIStyle();
		titleStyle.normal.textColor = Color.black;
		titleStyle.fontSize = (int)(75 * Util.GetScale());
		
		GUILayout.BeginArea(new Rect(0f, 0f, Screen.width, Screen.height * 0.25f));
		GUILayout.BeginVertical();
		GUILayout.FlexibleSpace();
		GUILayout.BeginHorizontal();
		GUILayout.FlexibleSpace();
		GUILayout.Label("Iron Strife RTS", titleStyle);
		GUILayout.FlexibleSpace();
		GUILayout.EndHorizontal();
		GUILayout.FlexibleSpace();
		GUILayout.EndVertical();
		GUILayout.EndArea();
	}
	#endregion
}