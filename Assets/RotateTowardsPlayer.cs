using UnityEngine;
using System.Collections;

public class RotateTowardsPlayer : MonoBehaviour {

	Transform player;

	// Use this for initialization
	void Start () {
		player = GameObject.Find ("FPSController").transform;
	}
	
	// Update is called once per frame
	void Update () {
		transform.LookAt(2 * transform.position - player.position);
	}
}
