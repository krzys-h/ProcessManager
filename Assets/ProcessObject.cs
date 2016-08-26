using UnityEngine;
using System.Collections;

public class ProcessObject : MonoBehaviour {

	public int processId;
	public int playerId;
	public Color color;

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
		transform.localScale = new Vector3 (x, x, x);
		transform.position = new Vector3(transform.position.x, x / 2, transform.position.z);
		gameObject.GetComponent<Renderer>().material.color = color;
	}
}
