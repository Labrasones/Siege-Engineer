﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class OpenWall02Upgrade : BaseUpgrade {

	protected override void OnApplyUpgrade (GameObject playerObject)
	{
		Destroy(GameObject.Find("WallGate02"));
	} 

	protected override void OnRemoveUpgrade (GameObject playerObject)
	{

	}

}
