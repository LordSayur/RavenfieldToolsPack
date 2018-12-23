using UnityEngine;
using UnityEditor;
using System.Collections;

[CustomEditor(typeof(SpawnPointNeighborManager))]
public class SpawnPointNeighborManagerEditor : Editor {

	SpawnPointNeighborManager neighborManager;

	void OnEnable() {
		this.neighborManager = (SpawnPointNeighborManager) this.target;
	}

	void OnSceneGUI() {

		if(this.neighborManager.neighbors == null) return;

		foreach(SpawnPointNeighborManager.SpawnPointNeighbor neighbor in this.neighborManager.neighbors) {
			if(neighbor.a != null && neighbor.b != null) {
				Handles.color = Color.magenta;
				Handles.DrawLine(neighbor.a.transform.position, neighbor.b.transform.position);
				Vector3 center = (neighbor.a.transform.position + neighbor.b.transform.position)*0.5f;

				if(neighbor.waterConnection) {
					Handles.color = Color.blue;
					Handles.SphereHandleCap(-1, center-Vector3.up*5f, Quaternion.identity, 4f, EventType.Ignore);
				}

				if(neighbor.landConnection) {
					Handles.color = Color.green;
					Handles.SphereHandleCap(-1, center+Vector3.up*5f, Quaternion.identity, 4f, EventType.Ignore);
				}
			}
		}
	}
}