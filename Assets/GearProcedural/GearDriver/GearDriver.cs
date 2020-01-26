// GearDriver.cs (C)2015 by Alexander Schlottau, Hamburg, Germany
//   simulates procedural gear and worm gear objects at runtime.


// use 'motorSpeedRPM' to set speed of motor gear from other scripts, events or playmaker 
//  during gameplay:
//
//  For Playmaker:
//   Add a FSM to the motor gear game object.
//   use an Action -> Unity Object -> Set Property
//   As Target object select 'gear'
//	 As Property select 'motorSpeedRPM'
//   Then set a value for the speed or link another variable you stored in playmaker

// Read or Set by (other) scripts:
//
//   GearDriver gd = (GearDriver)anotherGameObject.GetComponent(typeof(GearDriver));
//   gd.motorSpeedRPM = 15.0f;
//   float speed = gd.motorSpeedRPM;
//
// There are some public methods you can call from other scripts or playmaker to
// connect and disconnect gears at runtime.
// You find these methods from line 150.



using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class GearDriver : MonoBehaviour {

	[Serializable]
	public class Settings {

		public bool isMotor, isShaft, isWorm = false, invWormOut = false;
		public bool updateOnce = false;
		public bool updateLive = false;
		public float motorSpeed = 0.0f;
		public List<GearDriver> outputTo;
	}

	public Settings settings;


	public float motorSpeedRPM {
		get { return actualSpeed / 6.0f;}
		set { if (settings.isMotor)
				settings.motorSpeed = value * 6.0f; }
	}

	private float actualSpeed = 0.0f;
	private int error = 0;
	private bool lastUpdState = false;

	void Start () {
		
		if (GetComponent<ProceduralWormGear> () != null) 
			settings.isWorm = true;
		else
			if (GetComponent<ProceduralGear> () == null)
				settings.isShaft = true;
			else
				settings.isShaft = false;
		
		if (settings.isMotor) {
			error++;
			UpdateConnections ( settings.isShaft? 0 : GetTeethCountFromGearScript(), settings.motorSpeed, error, settings.updateLive);
		}
	}
	
	private int GetTeethCountFromGearScript() {
		
		if (GetComponent<ProceduralWormGear> () != null)
			if (GetComponent<ProceduralWormGear> ().prefs.lr)
				return settings.invWormOut?1:-1;
			else
				return settings.invWormOut?-1:1;
		else
			if (GetComponent<ProceduralGear> () != null)
				return GetComponent<ProceduralGear> ().prefs.teethCount;
			else
				return 0;
	}

	void Update () {
	
		if (settings.isMotor)
			DriveMotor ();
		
		if (!settings.updateLive)
			gameObject.transform.Rotate (Vector3.up * Time.deltaTime * -actualSpeed);
	}

	private void DriveMotor() {

		if (settings.updateOnce || settings.updateLive || lastUpdState)
			UpdatePowerchain ();

	}
	
	public void UpdateConnections(int _otherTeethCount, float _speed, int _error, bool _updateRotation) {

        if (!this.enabled) return;

		if (!settings.isMotor) {
			if (error == _error) {
				Debug.LogWarning ("GearDriver.cs : Get two inputs on " + gameObject.name + " . Check connections for loop.");
				this.enabled = false;
				return;
			}
			settings.updateLive = _updateRotation;
		}
		error = _error;

		int tc = 0;
		if (_otherTeethCount == 0) {
			if (!settings.isShaft)
				_otherTeethCount = -GetTeethCountFromGearScript(); 
			else
				_otherTeethCount = 1;
		}
		if (!settings.isShaft) {
			tc = GetTeethCountFromGearScript(); 
			actualSpeed = (float)_otherTeethCount / (float)tc * -_speed;
		}
		else
			actualSpeed = _speed;

		for (int i = 0; i < settings.outputTo.Count; i++) {
			if (settings.outputTo[i] != null) {
				settings.outputTo[i].UpdateConnections (tc, actualSpeed, error, _updateRotation);
			} else {
				settings.outputTo.RemoveAt(i);
			}
		}

		if (_updateRotation)
			gameObject.transform.Rotate (Vector3.up * Time.deltaTime * -actualSpeed);
	}
	
	public void UpdatePowerchain() {

		error = error>8?0:error+=2;
		lastUpdState = settings.updateLive;
		UpdateConnections (settings.isShaft? 0 : GetTeethCountFromGearScript(), settings.motorSpeed, error, lastUpdState);
		settings.updateOnce = false;
	}

	// public methods to call from events, Buttons, Animation Events or other scripts:
    //
	// to work "live update" on the motor gear has to be set true !!!


	// Connect a gear by its gearDriver script as reference
	//
	// Call from other scripts:
	//	  anotherGameObject.GetComponent<GearDriver>().ConnectGear(gearDriver);
	//
	public void ConnectGear(GearDriver gearDriver) {
	
		if (gearDriver != null)
			settings.outputTo.Add (gearDriver);
	}

	// Disconnects all gears from the "connected outputs" lists of this gear
	//
	// Call from other scripts:
	//	  anotherGameObject.GetComponent<GearDriver>().DisconnectAllGears();
	//
	public void DisconnectAllGears() {
	
		settings.outputTo.Clear ();
	}

	// Remove a gear from the "Connected Outputs" list (with the gearDriver script as reference)
	//
	// Call from other scripts:
	//	  anotherGameObject.GetComponent<GearDriver>().DisconnectGear(gearDriver);
	//
	public void DisconnectGear(GearDriver gearDriver) {

		if (gearDriver != null)
			settings.outputTo.Remove (gearDriver);
	}

	// Remove a gear from the "Connected Outputs" list of this gear by index
	//
	// Call from other scripts:
	//	  anotherGameObject.GetComponent<GearDriver>().DisconnectGearByIndex(5);
	//	  
	public void DisconnectGearByIndex (int index) {

		if (index < settings.outputTo.Count)
			settings.outputTo.RemoveAt (index);
	}

	// Disconnects this gear from the "Connected Outputs" lists of all other gears
	//
	// Call from other scripts:
	//	  anotherGameObject.GetComponent<GearDriver>().DisconnectFromAllOther();
	//	  
	public void DisconnectFromAllOther() {

		foreach (GearDriver gd in GameObject.FindObjectsOfType<GearDriver>()) {
			if (gd.settings.outputTo.Contains (this))
				gd.settings.outputTo.Remove (this);
		}
	}

	// Disconnects a gear from the connected output lists of all other gears by its name
	//
	// Call from other scripts:
	//	  GameObject.FindObjectOfType<GearDriver>().DisconnectFromAllByName("NameOfGearToDisconnect");
	//
	public void DisconnectFromAllByName(string name) {
		
		GameObject go = GameObject.Find (name);
		if (go == null)
			return;
		GearDriver gd = go.GetComponent<GearDriver> ();
		if (gd == null)
			return;
		foreach (GearDriver g in GameObject.FindObjectsOfType<GearDriver>()) {
			if (g.settings.outputTo.Contains (gd))
				g.settings.outputTo.Remove (gd);
		}
	}

	// Connects a gear to another by their names
	//
	// Call from other scripts: 
	//   GameObject.Find("gearThatDrives").GetComponent<GearDriver>().ConnectGearByName("GearToConnect");
	//
	public void ConnectGearByName(string name) {
	
		GameObject go = GameObject.Find (name);
		if (go == null)
			return;
		GearDriver gd = go.GetComponent<GearDriver> ();
		if (gd == null)
			return;
		settings.outputTo.Add (gd);
	}

	// Disconnects a gear from another by their names
	//
	// Call from other scripts: 
	//   GameObject.Find("gearThatDrives").GetComponent<GearDriver>().DisconnectGearByName("GearToDisconnect");
	//
	public void DisconnectGearByName(string name) {

		GameObject go = GameObject.Find (name);
		if (go == null)
			return;
		GearDriver gd = go.GetComponent<GearDriver> ();
		if (gd == null)
			return;
		settings.outputTo.Remove (gd);
	}
}
