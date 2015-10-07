﻿using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using System.Collections.Generic;

namespace TouchScript.Examples.UI
{
	public class SetColor : MonoBehaviour 
	{
		public List<Color> Colors;

		public void Set(int id) 
		{
			GetComponent<Image>().color = Colors[id];
		}

	}
}