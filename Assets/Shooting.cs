using UnityEngine;
using System.Collections;

public class Shooting : MonoBehaviour {
	public GameObject bulletPrefab;

	// Use this for initialization
	void Start () {
	
	}
	
	// Update is called once per frame
	void Update () {
		if(Input.GetButtonDown("Fire1")) {
			Transform spawn = transform.Find ("BulletSpawn");
			GameObject bullet = (GameObject) GameObject.Instantiate(bulletPrefab, spawn.position, spawn.rotation);
			bullet.GetComponent<Rigidbody>().velocity = bullet.transform.forward * 25;
			Destroy(bullet, 2.0f);
		}
	}
}
