﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using UnityEngine.Networking;

public class ProcessManager : MonoBehaviour {
	public GameObject processObjectPrefab;

	[System.Serializable]
	public class ProcessData {
		public string name;
		public long memory;
	}

	[System.Serializable]
	public class PeerData {
		public string address;
		public int port;
	}

	const byte MESSAGE_PROCESS_LIST = 1;
	const byte MESSAGE_KILL_PROCESS = 2;
	const byte MESSAGE_GET_PEERS = 3;
	const byte MESSAGE_PEERS = 4;

	int channelId;
	int hostId = -1;

	PlayerProcessManager localProcessManager;
	Dictionary<int, PlayerProcessManager> playerProcessManagers = new Dictionary<int, PlayerProcessManager>();
	Dictionary<int, ProcessData> localProcessList = new Dictionary<int, ProcessData> ();
	
	int serverPort = 8888;

	void Start () {
		NetworkTransport.Init();

		localProcessManager = CreateProcessManager (0);
		localProcessManager.peerId = (int)(Random.value * 65535);
		
		StartServer ();

		string saved = PlayerPrefs.GetString ("LastConnected");
		if (saved != "") {
			string[] savedSplit = saved.Split(new char[] { ':' }, 2);
			remoteIp = savedSplit[0];
			remotePort = savedSplit[1];
		}

		InvokeRepeating ("UpdateProcesses", 0, 0.25f);
	}

	void StartServer()
	{
		Debug.LogError ("Start server");
		ConnectionConfig config = new ConnectionConfig ();
		channelId = config.AddChannel (QosType.ReliableFragmented);
		HostTopology topology = new HostTopology (config, 10);
		for (serverPort = 8888; ; serverPort++) {
			Debug.Log ("Try port "+serverPort);
			hostId = NetworkTransport.AddHost (topology, serverPort);
			if (hostId >= 0) break;
		}
		Debug.LogError ("Server started on " + serverPort);
	}
	
	void StartClient()
	{
		PlayerPrefs.SetString ("LastConnected", remoteIp+":"+remotePort);
		Debug.LogError ("Connect: " + remoteIp + ":" + remotePort);
		byte error = 0;
		NetworkTransport.Connect(hostId, remoteIp, System.Int32.Parse(remotePort), 0, out error);
		if (error != 0) {
			Debug.LogError ("Connection failed "+error);
			return;
		}
	}

	PlayerProcessManager CreateProcessManager(int connectionId)
	{
		GameObject obj = new GameObject ();
		obj.name = "PlayerProcessManager[" + connectionId + "]";
		obj.transform.parent = this.transform;
		PlayerProcessManager procMan = obj.AddComponent<PlayerProcessManager> ();
		procMan.processObjectPrefab = processObjectPrefab;
		procMan.playerId = connectionId;
		playerProcessManagers.Add(connectionId, procMan);
		return procMan;
	}

	void DestroyProcessManager(int connectionId)
	{
		GameObject.Destroy (playerProcessManagers [connectionId].gameObject);
		playerProcessManagers.Remove (connectionId);
	}
	
	byte[] recBuffer = new byte[16384];
	void Update () {
		int recHostId;
		int recConnectionId;
		int dataSize;
		byte error;
		NetworkEventType recData = NetworkTransport.Receive(out recHostId, out recConnectionId, out channelId, recBuffer, recBuffer.Length, out dataSize, out error);
		switch (recData)
		{
		case NetworkEventType.Nothing:
			break;
		case NetworkEventType.ConnectEvent:
			Debug.Log ("Connected");
			CreateProcessManager(recConnectionId);
			MemoryStream sendstream2 = new MemoryStream();
			sendstream2.WriteByte (MESSAGE_GET_PEERS);
			new BinaryFormatter().Serialize(sendstream2, localProcessManager.peerId);
			byte[] b2 = sendstream2.ToArray ();
			NetworkTransport.Send (hostId, recConnectionId, channelId, b2, b2.Length, out error);
			break;
		case NetworkEventType.DataEvent:
			//Debug.Log ("DATA "+dataSize);
			MemoryStream stream = new MemoryStream(recBuffer, 0, dataSize);
			byte type = (byte)stream.ReadByte();
			switch(type) {
			case MESSAGE_PROCESS_LIST:
				Dictionary<int, ProcessData> processes = (Dictionary<int, ProcessData>) new BinaryFormatter().Deserialize(stream);
				playerProcessManagers[recConnectionId].UpdateProcesses(processes);
				break;
			case MESSAGE_KILL_PROCESS:
				int processId = (int) new BinaryFormatter().Deserialize(stream);
				KillLocalProcess(processId);
				break;
			case MESSAGE_GET_PEERS:
				Debug.LogError ("Send peer list");
				int remotePeerId = (int) new BinaryFormatter().Deserialize(stream);
				foreach (KeyValuePair<int, PlayerProcessManager> player in playerProcessManagers) {
					if(remotePeerId == player.Value.peerId) {
						Debug.LogError ("Duplicate connection "+recConnectionId+" with player "+player.Key+" detected, disconnect");
						NetworkTransport.Disconnect(hostId, recConnectionId, out error);
					}
				}
				playerProcessManagers[recConnectionId].peerId = remotePeerId;
				Dictionary<int, PeerData> peers = new Dictionary<int, PeerData>();
				foreach (KeyValuePair<int, PlayerProcessManager> player in playerProcessManagers) {
					if(player.Key == 0) continue; // ignore local player
					if(player.Key == recConnectionId) continue; // ignore remote player
					PeerData peer = new PeerData();
					UnityEngine.Networking.Types.NetworkID network;
					UnityEngine.Networking.Types.NodeID dstNode;
					NetworkTransport.GetConnectionInfo(hostId, player.Key, out peer.address, out peer.port, out network, out dstNode, out error);
					peers.Add(player.Key, peer);
				}
				MemoryStream sendstream = new MemoryStream();
				sendstream.WriteByte (MESSAGE_PEERS);
				new BinaryFormatter().Serialize(sendstream, peers);
				byte[] b = sendstream.ToArray ();
				NetworkTransport.Send (hostId, recConnectionId, channelId, b, b.Length, out error);
				break;
			case MESSAGE_PEERS:
				Debug.LogError ("Recieve peer list");
				Dictionary<int, PeerData> remotepeers = (Dictionary<int, PeerData>) new BinaryFormatter().Deserialize(stream);
				foreach (KeyValuePair<int, PeerData> peer in remotepeers) {
					Debug.LogError ("Connect to peer: "+peer.Value.address+":"+peer.Value.port);
					NetworkTransport.Connect(hostId, peer.Value.address, peer.Value.port, 0, out error);
				}
				break;
			}
			break;
		case NetworkEventType.DisconnectEvent:
			Debug.Log ("Disconnected");
			DestroyProcessManager(recConnectionId);
			break;
		}

		if (Input.GetKeyDown (KeyCode.C))
			StartClient ();
	}

	string remoteIp = "127.0.0.1";
	string remotePort = "8888";

	void OnGUI() {
		GUILayout.Label ("ProcessManager by krzys_h v0.4-beta");
		GUILayout.Label (hostId >= 0 ? "Server running on :" + serverPort : "Server not running");
		remoteIp = GUILayout.TextField (remoteIp);
		remotePort = GUILayout.TextField (remotePort);
		if (GUILayout.Button ("Connect [C]"))
			StartClient ();

		string s = "";
		foreach (KeyValuePair<int, PlayerProcessManager> player in playerProcessManagers) {
			if(player.Key == 0) {
				s += player.Key + ": 127.0.0.1:"+serverPort;
			} else {
				string address;
				int port;
				UnityEngine.Networking.Types.NetworkID network;
				UnityEngine.Networking.Types.NodeID dstNode;
				byte error;
				NetworkTransport.GetConnectionInfo(hostId, player.Key, out address, out port, out network, out dstNode, out error);
				s += "\n" + player.Key + ": " + address + ":" + port;
			}
		}
		GUILayout.Label (s);
	}
	
	public void UpdateProcesses () {
		UpdateLocalProcessList ();
		localProcessManager.UpdateProcesses (localProcessList);
		
		MemoryStream stream = new MemoryStream();
		stream.WriteByte (MESSAGE_PROCESS_LIST);
		new BinaryFormatter().Serialize(stream, localProcessList);
		byte[] b = stream.ToArray ();

		foreach (KeyValuePair<int, PlayerProcessManager> player in playerProcessManagers) {
			if (player.Key == 0) continue; // ignore local player
			byte error;
			NetworkTransport.Send (hostId, player.Key, channelId, b, b.Length, out error);
			if (error != 0) {
				Debug.LogError ("Send error: "+error);
				NetworkTransport.Disconnect(hostId, player.Key, out error);
			}
		}
	}

	public int GetProcessOwner(string procfile)
	{
		using (StreamReader sr = new StreamReader(procfile+"/status"))
		{
			while (sr.Peek() >= 0) {
				string line = sr.ReadLine();
				if(line.StartsWith("Uid:")) {
					string[] split = line.Split('\t');
					int uid = System.Int32.Parse(split[1]);
					return uid;
				}
			}
		}
		Debug.LogError ("No owner found for "+procfile);
		return 0;
	}
	
	public int GetProcessOwner(int processId)
	{
		return GetProcessOwner ("/proc/"+processId);
	}

	public string GetProcessName(string procfile)
	{
		using (StreamReader sr = new StreamReader(procfile+"/cmdline"))
		{
			while (sr.Peek() >= 0) {
				string line = sr.ReadLine();
				string[] path = line.Split('\0')[0].Split(' ')[0].Split('/');
				return path[path.Length-1];
			}
		}
		//Debug.LogError ("No name found for "+procfile);
		return "???";
	}
	
	public string GetProcessName(int processId)
	{
		return GetProcessName ("/proc/"+processId);
	}
	
	void UpdateLocalProcessList()
	{
		int owner = GetProcessOwner ("/proc/self");
		System.Diagnostics.Process[] running = System.Diagnostics.Process.GetProcesses();
		Dictionary<int, bool> newProcessList = new Dictionary<int, bool>(); //TODO: set?
		foreach (System.Diagnostics.Process process in running)
		{
			try
			{
				if (!process.HasExited)
				{
					ProcessData processData;
					if (!localProcessList.ContainsKey(process.Id)) {
						if (GetProcessOwner(process.Id) != owner && owner != 0) continue;
						processData = new ProcessData();
						processData.name = GetProcessName(process.Id);
						if (processData.name == "???") continue; // ignore zombie processes
						localProcessList.Add(process.Id, processData);
					} else processData = localProcessList[process.Id];
					newProcessList.Add(process.Id, true);
					//processData.name = process.ProcessName;
					processData.memory = process.VirtualMemorySize64;
				}
			}
			catch (System.InvalidOperationException)
			{
				Debug.Log ("InvalidOperationException: "+process.Id);
			}
		}
		List<int> toRemove = new List<int> ();
		foreach (KeyValuePair<int, ProcessData> process in localProcessList) {
			if (!newProcessList.ContainsKey(process.Key)) {
				toRemove.Add(process.Key);
			}
		}
		foreach (int key in toRemove) {
			localProcessList.Remove(key);
		}
	}

	public void KillProcess(ProcessObject processObject)
	{
		if (processObject.playerId == 0) {
			KillLocalProcess (processObject.processId);
		} else {
			KillRemoteProcess(processObject.playerId, processObject.processId);
		}
	}

	void KillLocalProcess(int processId)
	{
		System.Diagnostics.Process process = System.Diagnostics.Process.GetProcessById (processId);
		process.Kill ();
	}

	void KillRemoteProcess(int playerId, int processId)
	{
		MemoryStream stream = new MemoryStream();
		stream.WriteByte (MESSAGE_KILL_PROCESS);
		new BinaryFormatter().Serialize(stream, processId);
		byte[] b = stream.ToArray ();
		byte error;
		NetworkTransport.Send (hostId, playerId, channelId, b, b.Length, out error);
	}
}
