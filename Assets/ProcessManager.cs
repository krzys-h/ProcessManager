using UnityEngine;
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

	const byte MESSAGE_PROCESS_LIST = 1;
	const byte MESSAGE_KILL_PROCESS = 2;

	int channelId;
	int hostId;

	PlayerProcessManager localProcessManager;
	Dictionary<int, PlayerProcessManager> playerProcessManagers = new Dictionary<int, PlayerProcessManager>();
	Dictionary<int, ProcessData> localProcessList = new Dictionary<int, ProcessData> ();

	void Start () {
		NetworkTransport.Init();

		localProcessManager = CreateProcessManager (0);

		InvokeRepeating ("UpdateProcesses", 0, 1.0f);
	}

	void StartServer()
	{
		Debug.LogError ("Start server");
		ConnectionConfig config = new ConnectionConfig();
		channelId = config.AddChannel(QosType.ReliableFragmented);
		HostTopology topology = new HostTopology(config, 10);
		hostId = NetworkTransport.AddHost(topology, System.Int32.Parse(serverPort));
	}
	
	void StartClient()
	{
		Debug.LogError ("Connect: " + remoteIp);
		byte error = 0;
		/*int connectionId =*/ NetworkTransport.Connect(hostId, remoteIp, 8888, 0, out error);
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
			}
			break;
		case NetworkEventType.DisconnectEvent:
			Debug.Log ("Disconnected");
			DestroyProcessManager(recConnectionId);
			break;
		}

		if (Input.GetKeyDown (KeyCode.X))
			StartServer ();
		if (Input.GetKeyDown (KeyCode.C))
			StartClient ();
	}

	string remoteIp = "127.0.0.1";
	string serverPort = "8888";

	void OnGUI() {
		remoteIp = GUILayout.TextField (remoteIp);
		serverPort = GUILayout.TextField (serverPort);
		if (GUILayout.Button("Server [X]"))
			StartServer();
		if (GUILayout.Button("Connect [C]"))
			StartClient();
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
			if (error != 0) Debug.LogError ("Send error: "+error);
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
					if (!localProcessList.ContainsKey(process.Id)) {
						if (GetProcessOwner(process.Id) != owner && owner != 0) continue;
						localProcessList.Add(process.Id, new ProcessData());
					}
					ProcessData processData = localProcessList[process.Id];
					newProcessList.Add(process.Id, true);
					//processData.name = process.ProcessName;
					processData.name = GetProcessName(process.Id);
					processData.memory = process.VirtualMemorySize64;
				}
			}
			catch (System.InvalidOperationException)
			{
				Debug.Log ("InvalidOperationException: "+process.Id);
			}
		}
		foreach (KeyValuePair<int, ProcessData> process in localProcessList) {
			if (!newProcessList.ContainsKey(process.Key)) {
				localProcessList.Remove(process.Key);
			}
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
