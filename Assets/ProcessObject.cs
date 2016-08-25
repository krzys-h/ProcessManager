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
	}

	void Update () {
	}
}
