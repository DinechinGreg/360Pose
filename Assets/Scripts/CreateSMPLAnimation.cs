using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using SimpleJSON;

public class CreateSMPLAnimation : MonoBehaviour 
{
	private float _fps = 30f;
	private int _frameSkip = 10;
	private int _numberOfFrames = 402;
	private float[][] _translations;
	private float[][][] _poses;
	private Transform _avatarTransform;
	private SMPLBlendshapes _blendshapes;
	private AnimationClip _clip;
	private Dictionary<Transform, AnimationCurve[]> _dictPathToCurve;

	//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	IEnumerator Start () 
	{
		InitializeAvatar ();
		yield return null;
		GetJSONInformation ();
		_clip = new AnimationClip();
		_clip.legacy = true;
		InitializeDictionary ();
		for (int i = 0; i < _numberOfFrames; i++) 
		{
			SetAvatarPoseForFrame (i);
			yield return null;
			SetAnimationFromAvatarPose (_frameSkip*i/_fps);
		}
		SaveToClip ();
		AssetDatabase.CreateAsset(_clip, "Assets/Resources/Animation.anim");
		Debug.Log ("Finished!");
	}

	//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	private void InitializeAvatar()
	{
		GameObject avatarPrefab = Resources.Load<GameObject> ("Prefabs/AvatarPrefab");
		_avatarTransform = GameObject.Instantiate (avatarPrefab).transform;
		_blendshapes = _avatarTransform.GetComponentInChildren<SMPLBlendshapes> ();
	}

	//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	private void GetJSONInformation()
	{
		float[] averageShape = new float[10];
		_translations = new float[_numberOfFrames][];
		_poses = new float[_numberOfFrames][][];
		for(int frameIndex = 0; frameIndex < _numberOfFrames; frameIndex++)
		{
			_translations[frameIndex] = new float[3];
			_poses[frameIndex] = new float[24][];
			string path = Application.dataPath + "/../Python/ThirdParty/hmr/results/" + frameIndex.ToString() + ".json";
			using (StreamReader reader = new StreamReader (path))
			{
				string text = reader.ReadToEnd ();
				reader.Close ();
				JSONNode node = JSON.Parse (text);
				for (int i = 0; i < 10; i++)
					averageShape [i] += node ["shape"] [i].AsFloat / _numberOfFrames;
				for (int i = 0; i < 3; i++)
					_translations [frameIndex] [i] = node ["translation"] [i].AsFloat;
				_translations [frameIndex] [0] = -Mathf.Abs (_translations [frameIndex] [0]);
				for (int i = 0; i < 24; i++) 
				{
					_poses [frameIndex] [i] = new float[4];
					for (int j = 0; j < 4; j++)
						_poses [frameIndex] [i] [j] = node ["pose"] [i] [j].AsFloat;
				}
			}
		}
		_blendshapes.changeShapeParams (averageShape);
	}

	//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	private void SetAvatarPoseForFrame(int frameIndex)
	{
		_blendshapes.getModifyBones ().ResetAll ();
		_avatarTransform.rotation = Quaternion.identity;
		_blendshapes.getModifyBones ().updateBoneAngles (_poses [frameIndex], _translations [frameIndex]);
	}

	//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	private void InitializeDictionary()
	{
		_dictPathToCurve = new Dictionary<Transform, AnimationCurve[]> ();
		foreach (Transform child in _avatarTransform.GetComponentsInChildren<Transform>())
		{
			if (child == _avatarTransform)
				continue;
			AnimationCurve[] animationCurveArray = new AnimationCurve[7];
			for (int i = 0; i < animationCurveArray.Length; i++) 
			{
				animationCurveArray [i] = new AnimationCurve ();
			}
			_dictPathToCurve.Add (child, animationCurveArray);
		}
	}

	//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	private void SetAnimationFromAvatarPose(float time)
	{
		foreach (Transform child in _avatarTransform.GetComponentsInChildren<Transform>())
		{
			if (child == _avatarTransform)
				continue;
			_dictPathToCurve [child] [0].AddKey (new Keyframe (time, child.localPosition.x));
			_dictPathToCurve [child] [1].AddKey (new Keyframe (time, child.localPosition.y));
			_dictPathToCurve [child] [2].AddKey (new Keyframe (time, child.localPosition.z));
			_dictPathToCurve [child] [3].AddKey (new Keyframe (time, child.localRotation.x));
			_dictPathToCurve [child] [4].AddKey (new Keyframe (time, child.localRotation.y));
			_dictPathToCurve [child] [5].AddKey (new Keyframe (time, child.localRotation.z));
			_dictPathToCurve [child] [6].AddKey (new Keyframe (time, child.localRotation.w));
		}
	}

	//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	private void SaveToClip()
	{
		foreach (Transform child in _avatarTransform.GetComponentsInChildren<Transform>())
		{
			if (child == _avatarTransform)
				continue;
			for(int i = 0; i < 7; i++)
			{
				for (int j = 0; j < _numberOfFrames; j++)
				{
					AnimationUtility.SetKeyLeftTangentMode (_dictPathToCurve [child] [i], j, AnimationUtility.TangentMode.ClampedAuto);
					AnimationUtility.SetKeyRightTangentMode (_dictPathToCurve [child] [i], j, AnimationUtility.TangentMode.ClampedAuto);
				}
			}
			string relativePath = child.name;
			Transform currentTransform = child;
			while (currentTransform.parent != _avatarTransform) 
			{
				relativePath = currentTransform.parent.name + "/" + relativePath;
				currentTransform = currentTransform.parent;
			}
			_clip.SetCurve (relativePath, typeof(Transform), "localPosition.x", _dictPathToCurve [child] [0]);
			_clip.SetCurve (relativePath, typeof(Transform), "localPosition.y", _dictPathToCurve [child] [1]);
			_clip.SetCurve (relativePath, typeof(Transform), "localPosition.z", _dictPathToCurve [child] [2]);
			_clip.SetCurve (relativePath, typeof(Transform), "localRotation.x", _dictPathToCurve [child] [3]);
			_clip.SetCurve (relativePath, typeof(Transform), "localRotation.y", _dictPathToCurve [child] [4]);
			_clip.SetCurve (relativePath, typeof(Transform), "localRotation.z", _dictPathToCurve [child] [5]);
			_clip.SetCurve (relativePath, typeof(Transform), "localRotation.w", _dictPathToCurve [child] [6]);
		}
		_clip.EnsureQuaternionContinuity ();
	}
}

