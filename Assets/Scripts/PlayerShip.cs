﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
/*
 * Ethan Zhu and Rachael H.
 */
public class PlayerShip : MonoBehaviour, IShipBase {

	// The axis used to take input.
	public Camera camera;
	public string inputAxis = "Horizontal";
	public float moveSpeed = 5f;
	public Rigidbody bullet;
	public Transform fireTransform;
	public int maxBullets = 7;
	public float fireCooldown = 0.2f;
	public MapLine curMapLine;
	public GameObject explodePrefab;
	public float moveCooldown = 0.0f;

	public AudioClip soundFire;
	public AudioClip soundDeath;
	public AudioClip soundZapper;

	public Canvas highlighter;

	public bool legacyMovement = false;

	[HideInInspector] public bool movingForward = false;

	// References to the MapManager and GameManager
	private MapManager _mapManager;
	private GameManager _gameManager;
	// The value of input, updated each frame.
	private float _inputValue;
	private Quaternion _desiredRotation;
	private int _curBullets;
	private float _lastFire;
	private Rigidbody _rigidbody;
	private float _godTimer;
	private AudioSource _audioSource;
	private bool _zapperReady;
	private MapLine _targetMapLine;
	private MapLine _nextMapLine;
	private float _nextMove;

	// Use this for initialization
	void Start () {
		_curBullets = 0;
		_rigidbody = GetComponent<Rigidbody> ();
		_mapManager = GameObject.Find ("MapManager").GetComponent<MapManager> ();
		_gameManager = GameObject.Find ("GameManager").GetComponent<GameManager> ();
		_godTimer = Time.fixedTime + 3;
		_audioSource = GetComponent<AudioSource> ();
		_zapperReady = true;
		_nextMove = Time.fixedTime + moveCooldown;
	}

	void OnEnable() {
		_godTimer = Time.fixedTime + 3;
	}

	// Update is called once per frame
	void Update () {
		Vector3 curDirVec = curMapLine.GetDirectionVector ();
		Vector3 newDirVec = new Vector3 (-curDirVec.y, curDirVec.x, 0);
		highlighter.transform.rotation = Quaternion.LookRotation(new Vector3(0f,0f,-1f), newDirVec);
		highlighter.transform.position = curMapLine.GetMidPoint() + new Vector3(0f,0f,20f);
		RectTransform rt = highlighter.GetComponent<RectTransform> ();
		rt.sizeDelta = new Vector2 (1.1f * curMapLine.GetLength (), 40);

	}

	void FixedUpdate(){

		if (curMapLine == null) {
			curMapLine = _mapManager.mapLines [2];
		}


		if (legacyMovement == true) {
			_inputValue = Input.GetAxis (inputAxis);


		} else {
			Vector3 mousePos = Input.mousePosition;
			_targetMapLine = curMapLine;
			foreach (MapLine ml in _mapManager.mapLines) {
				Vector3 MLPos = camera.WorldToScreenPoint (ml.GetMidPoint ());
				Vector3 curMLPos = camera.WorldToScreenPoint (_targetMapLine.GetMidPoint ());
				if (Vector3.Distance (mousePos, MLPos) < Vector3.Distance (mousePos, curMLPos)) {
					_targetMapLine = ml;
				}
			}
			int rightDist = curMapLine.getShortestDist (curMapLine.leftLine, _targetMapLine);
			int leftDist = curMapLine.getShortestDist (curMapLine.rightLine, _targetMapLine);
			if (leftDist < rightDist) {
				_nextMapLine = curMapLine.leftLine;
			} else if (leftDist >= rightDist && leftDist > 0) {
				_nextMapLine = curMapLine.rightLine;
			} else {
				_nextMapLine = curMapLine;
			}
			//curMapLine = _targetMapLine;
		}
		//if (_nextMove <= Time.fixedTime) {
			Move ();
		//	_nextMove = Time.fixedTime + moveCooldown;
		//}

		if ((Input.GetMouseButton(0) || Input.GetKey (KeyCode.Space)) && _lastFire + fireCooldown < Time.fixedTime && _curBullets < maxBullets) {
			Fire ();
			_lastFire = Time.fixedTime;
		}

		if ((Input.GetKey (KeyCode.LeftControl) || Input.GetMouseButton(1)) && _zapperReady == true) {
			Zapper ();
			_zapperReady = false;
		}
	}

	// Called each update to move sideways
	void Move(){
		if (legacyMovement == true) {
			Vector3 newPos;
			MapLine newMapLine;
			Quaternion newQuat;

			curMapLine.UpdateMovement (transform.position, Time.deltaTime * _inputValue * moveSpeed, out newPos, out newMapLine);

			if (movingForward == true) {
				newPos = newPos + new Vector3 (0f, 0f, transform.position.z + moveSpeed * 0.02f);
			}

			_rigidbody.MovePosition (newPos);

			if (newMapLine != null) {
				curMapLine = newMapLine;
			}
		} else {

			Vector3 newPos = _nextMapLine.GetMidPoint();
			if (movingForward == true) {
				newPos = newPos + new Vector3 (0f, 0f, transform.position.z + moveSpeed * 0.02f);
			}

			_rigidbody.MovePosition (newPos);

			Vector3 curDirVec = _nextMapLine.GetDirectionVector ();
			Vector3 newDirVec = new Vector3 (-curDirVec.y, curDirVec.x, 0);
			//print (Quaternion.Euler(newDirVec));
			_rigidbody.MoveRotation (Quaternion.LookRotation(new Vector3(0f,0f,1f), newDirVec));

			curMapLine = _nextMapLine;
		}
	}

	// Called to fire a projectile.
	public void Fire(){
		_curBullets++;
		_audioSource.clip = soundFire;
		_audioSource.Play ();
		Rigidbody shellInstance = Instantiate (bullet, fireTransform.position, fireTransform.rotation) as Rigidbody;
		shellInstance.GetComponent<PlayerBullet> ().SetShip (gameObject);
		shellInstance.velocity = 20f * (fireTransform.forward); 
	}

	void Zapper() {
		_audioSource.clip = soundZapper;
		_audioSource.Play ();
		GameObject[] enemies = GameObject.FindGameObjectsWithTag ("Enemy");
		foreach (GameObject enemy in enemies) {
			enemy.GetComponent<Flipper> ().TakeDamage (10);
		}
	}

	// Called when a projectile damages the ship. Should call OnDeath() if it kills;
	public void TakeDamage(int dmg){
		// GodTimer is when the ship is invincible
		if (Time.fixedTime < _godTimer)
			return;
			
		if (gameObject.activeSelf == false)
			return;
		// Since the player is dead on touch, just destroy it
		OnDeath();
	}

	// Called when the ship dies. Add points, do game state detection, etc.
	public void OnDeath(){
		GameObject newExplosion = Instantiate (explodePrefab, gameObject.transform.position, gameObject.transform.rotation);
		AudioSource explosionSource = newExplosion.GetComponent<AudioSource> ();
		explosionSource.clip = soundDeath;
		explosionSource.Play ();
		Destroy (newExplosion, 3f);
		gameObject.SetActive (false);
		_gameManager.OnPlayerDeath ();
	}

	public void BulletDestroyed() {
		_curBullets--;
	}

	public MapManager getMapManager() {
		return _mapManager;
	}
}
