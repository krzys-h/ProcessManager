using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PlayerProcessManager : MonoBehaviour {
	
	public GameObject processObjectPrefab;
	public int playerId;
	public int peerId;
	public Color color;
	private Dictionary<int, GameObject> objects = new Dictionary<int, GameObject>();

	// Use this for initialization
	void Start () {
		color = new Color (Random.value, Random.value, Random.value);
	}
	
	// Update is called once per frame
	void Update () {
	
	}

	public void UpdateProcesses(Dictionary<int, ProcessManager.ProcessData> processes) {
		foreach (KeyValuePair<int, ProcessManager.ProcessData> process in processes) {
			if (!objects.ContainsKey(process.Key)) {
				Debug.Log("Create object for [" + playerId + " : " + process.Key + "] " + process.Value.name);
				GameObject gameObject = Instantiate(processObjectPrefab);
				gameObject.name = "[" + playerId + " : " + process.Key + "] " + process.Value.name;
				gameObject.transform.parent = this.transform;
				gameObject.transform.position = new Vector3(Random.Range(-50.0F, 50.0F), 1F, Random.Range(-50.0F, 50.0F));
				ProcessObject processObject = gameObject.GetComponent<ProcessObject>();
				processObject.playerId = playerId;
				processObject.processId = process.Key;
				processObject.color = color;
				objects.Add(process.Key, gameObject);
			}
			objects[process.Key].GetComponent<ProcessObject>().UpdateProcess(process.Value);
		}
		List<int> toRemove = new List<int>();
		foreach (KeyValuePair<int, GameObject> pair in objects) {
			if (!processes.ContainsKey(pair.Key)) {
				GameObject.Destroy(pair.Value);
				toRemove.Add(pair.Key);
			}
		}
		foreach (int key in toRemove) {
			objects.Remove(key);
		}
	}
}
