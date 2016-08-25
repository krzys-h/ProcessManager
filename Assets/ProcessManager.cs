using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;

public class ProcessManager : MonoBehaviour {
	public GameObject processObjectPrefab;
	private Dictionary<int, GameObject> objects = new Dictionary<int, GameObject>();

	void Start () {
		InvokeRepeating ("UpdateProcesses", 0, 0.1f);
	}

	void Update () {
	}

	public void UpdateProcesses () {
		int owner = GetProcessOwner ("/proc/self/status");
		System.Diagnostics.Process[] running = System.Diagnostics.Process.GetProcesses();
		Dictionary<int, System.Diagnostics.Process> processMap = new Dictionary<int, System.Diagnostics.Process>();
		foreach (System.Diagnostics.Process process in running) {
			try
			{
				if (!process.HasExited)
				{
					processMap.Add(process.Id, process);
					if (!objects.ContainsKey(process.Id))
					{
						if (GetProcessOwner(process.Id) != owner && owner != 0) continue;
						Debug.Log("Create object for [" + process.Id + "] " + process.ProcessName);
						GameObject gameObject = Instantiate(processObjectPrefab);
						gameObject.name = "[" + process.Id + "] " + process.ProcessName;
						gameObject.transform.parent = this.transform;
						gameObject.transform.position = new Vector3(Random.Range(-50.0F, 50.0F), 1F, Random.Range(-50.0F, 50.0F));
						ProcessObject processObject = gameObject.GetComponent<ProcessObject>();
						processObject.setProcess(process);
						objects.Add(process.Id, gameObject);
					}
				}
			}
			catch (System.InvalidOperationException)
			{
				//Debug.Log("***** InvalidOperationException was caught!");
			}
		}
		foreach (KeyValuePair<int, GameObject> pair in objects) {
			if (!processMap.ContainsKey(pair.Key)) {
				GameObject.Destroy(pair.Value);
				objects.Remove(pair.Key);
			}
		}
	}

	/*public string GetProcessOwner(int processId)
	{
		string query = "Select * From Win32_Process Where ProcessID = " + processId;
		ManagementObjectSearcher searcher = new ManagementObjectSearcher(query);
		ManagementObjectCollection processList = searcher.Get();

		foreach (ManagementObject obj in processList)
		{
			string[] argList = new string[] { string.Empty, string.Empty };
			int returnVal = Convert.ToInt32(obj.InvokeMethod("GetOwner", argList));
			if (returnVal == 0)
			{
				// return DOMAIN\user
				return argList[1] + "\\" + argList[0];
			}
		}
		
		return "NO OWNER";
	}*/

	public int GetProcessOwner(string procfile)
	{
		using (StreamReader sr = new StreamReader(procfile))
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
		return GetProcessOwner ("/proc/"+processId+"/status");
	}
}
