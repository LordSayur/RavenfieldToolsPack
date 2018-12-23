using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

public class ContentExporter : MonoBehaviour {

	public const string PATH = MapExport.PATH;

	//static GameObject lastExportedContentModObject = null;

	[MenuItem("Ravenfield Tools/Export Map or Content Mod %E")]
	static void ExportSelected() {
		if(IsContentModObject(Selection.activeGameObject)) {
			ExportContent();
		}
		else if(FindObjectOfType<SpawnPointNeighborManager>() != null) {
			MapExport.ExportMap();
		}
		else {
			EditorUtility.DisplayDialog("Nothing to export", "There is no open map or content mod to export", "Ok");
		}
	}

	[MenuItem("Ravenfield Tools/Test Map or Content Mod %T")]
	static void TestSelected() {

		if(IsContentModObject(Selection.activeGameObject)) {
			string contentModPath = "";
			bool ok = ExportContentModObject(Selection.activeGameObject, out contentModPath);

			if(ok) {
				string parameters = "-nointro -noworkshopmods -nocontentmods \"-testcontentmod "+contentModPath+"\" \"-map Vehicle Testing\"";
				Paths.LaunchGame(parameters);
			}
			else {
				EditorUtility.DisplayDialog("Export failed", "Content mod couldn't be built. Please see the console for error messages.", "Ok");
			}
		}
		else if(FindObjectOfType<SpawnPointNeighborManager>() != null) {
			if(!Paths.HasExecutablePath()) {
				EditorUtility.DisplayDialog("Could not export map", "No executable set. Please find your game executable file with Ravenfield Tools -> Set Game Executable", "Ok");
				return;
			}

			string path;
			bool ok = MapExport.BuildBundle(BuildTarget.StandaloneWindows, out path, false);

			if(ok) {
				string parameters = "-nointro -noworkshopmods -nocontentmods \"-custommap "+path+"\"";
				Paths.LaunchGame(parameters);
			}
			else {
				EditorUtility.DisplayDialog("Export failed", "Map couldn't export properly. Please see the console for error messages.", "Ok");
			}
		}
		else {
			EditorUtility.DisplayDialog("Nothing to export", "There is no open map or content mod to export", "Ok");
		}
	}

	[MenuItem("Ravenfield Tools/Content/Export Content Mod")]
	public static void ExportContent() {
		string path = "";
		bool ok = ExportContentModObject(Selection.activeGameObject, out path);

		if(ok) {
			EditorUtility.DisplayDialog("Export completed", "Content mod was successfully exported to "+path, "Ok");
		}
		else {
			EditorUtility.DisplayDialog("Export failed", "Content mod couldn't be built. Please see the console for error messages.", "Ok");
		}
	}

	public static bool IsContentModObject(GameObject gameObject) {
		return gameObject != null && PrefabUtility.GetPrefabType(gameObject) == PrefabType.Prefab && (gameObject.GetComponent<WeaponContentMod>() != null || gameObject.GetComponent<VehicleContentMod>() != null);
	}

	public static bool ExportContentModObject(GameObject contentModObject, out string path) {

		path = "";

		if(!Paths.HasExecutablePath()) {
			EditorUtility.DisplayDialog("Could not export content mod", "No executable set. Please find your game executable file with Ravenfield Tools -> Set Game Executable", "Ok");
			return false;
		}

		if(!SanityCheck.DoSanityCheckContent()) {
			return false;
		}

		//lastExportedContentModObject = contentModObject;

		Object prefab = PrefabUtility.GetPrefabObject(contentModObject);

		return BuildBundle(BuildTarget.StandaloneWindows, contentModObject.name, prefab, out path);
	}

	public static bool BuildBundle(BuildTarget buildTarget, string name, Object contentPrefab, out string filepath) {

		filepath = "";

		Debug.ClearDeveloperConsole();
		Debug.Log("\n\n");
		Debug.Log("\n--- Building Content ---");

		AssetBundleBuild build = new AssetBundleBuild();

		if(!AssetDatabase.IsValidFolder(PATH)) {
			Debug.Log("No Export folder found, creating one.");
			AssetDatabase.CreateFolder("Assets", "Export");
		}

		try {
			build.assetBundleName = name+".rfc";

			List<string> assetNames = new List<string>();

			string assetPath = AssetDatabase.GetAssetPath(contentPrefab);
			assetNames.Add(assetPath);

			build.assetNames = assetNames.ToArray();

			AssetBundleBuild[] buildMap = new AssetBundleBuild[] {build};

			BuildPipeline.BuildAssetBundles(PATH, buildMap, BuildAssetBundleOptions.None, buildTarget);
		}
		catch(System.Exception e) {
			EditorUtility.DisplayDialog("Could not export content mod", "Could not create the .rfc file.\n\nDetails: "+e.Message, "Ok");
			Debug.LogException(e);
			return false;
		}

		Debug.Log("Successfully exported "+build.assetBundleName);

		if(Paths.HasExecutablePath()) {
			string exportPath = Paths.ProjectPath() + PATH + "/" + build.assetBundleName;

			string levelDestinationPath = Paths.ExecutableToolsStagingPath() + "/" + build.assetBundleName;

			try {
				File.Copy(exportPath, levelDestinationPath, true);
			}
			catch(System.Exception e) {
				EditorUtility.DisplayDialog("Could not export content mod", "Could not copy the .rfc file to the mod staging folder.\n\nDetails: "+e.Message, "Ok");
				Debug.LogException(e);
				return false;
			}

			Debug.Log("Copied exported content mod to "+levelDestinationPath);
			filepath = levelDestinationPath;
			return true;
		}

		return false;
	}

}
