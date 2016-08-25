using UnityEngine;
using System.Collections;

public class ProcessObject : MonoBehaviour {
	private System.Diagnostics.Process process;

	public void setProcess(System.Diagnostics.Process process) {
		this.process = process;
		this.transform.Find("Text").GetComponent<TextMesh>().text = "[" + process.Id + "] " + process.ProcessName;
	}

	public System.Diagnostics.Process getProcess() {
		return this.process;
	}

	void Start () {
		InvokeRepeating ("UpdateSize", 0, 0.1f);
	}

	void Update () {
	}

	void UpdateSize() {
		if (process == null)
			return;
		float x = ((float)process.VirtualMemorySize64) / 1024 / 1024 / 1024;
		if (x > 15)
			x = 15;
		this.transform.localScale = new Vector3 (x, x, x);
	}
}
