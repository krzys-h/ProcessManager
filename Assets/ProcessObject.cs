using UnityEngine;
using System.Collections;

public class ProcessObject : MonoBehaviour {

	public int processId;
	public int playerId;

	private TextMesh text;

	void Start () {
		text = transform.Find ("Text").GetComponent<TextMesh> ();
	}

	void Update () {
		if (text == null)
			return;
		text.text = gameObject.name;
	}

	public void UpdateProcess(ProcessManager.ProcessData process) {
		float x = ((float)process.memory) / 1024 / 1024 / 1024;
		if (x > 15)
			x = 15;
		this.transform.localScale = new Vector3 (x, x, x);
	}
}
