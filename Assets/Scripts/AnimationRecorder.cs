using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class AnimationRecorder : MonoBehaviour 
{
	private Transform _objectToRecord;
	private Dictionary<Transform, AnimationCurve[]> _dictTransformToAnimCurve;
	private Dictionary<Transform, Vector3> _dictLocalPositions;
	private Dictionary<Transform, Vector3> _dictWorldPositions;
	private Dictionary<Transform, Quaternion> _dictLocalRotations;

	//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	public void Initialize(Transform objectToRecord)
	{
		_objectToRecord = objectToRecord;
		_dictTransformToAnimCurve = new Dictionary<Transform, AnimationCurve[]> ();
		_dictLocalPositions = new Dictionary<Transform, Vector3> ();
		_dictWorldPositions = new Dictionary<Transform, Vector3> ();
		_dictLocalRotations = new Dictionary<Transform, Quaternion> ();
		foreach (Transform child in _objectToRecord.GetComponentsInChildren<Transform>())
		{
			AnimationCurve[] animationCurveArray = new AnimationCurve[7];
			for (int i = 0; i < animationCurveArray.Length; i++) 
			{
				animationCurveArray [i] = new AnimationCurve ();
			}
			_dictTransformToAnimCurve.Add (child, animationCurveArray);
			_dictLocalPositions.Add (child, 100f * Vector3.one);
			_dictWorldPositions.Add (child, 100f * Vector3.one);
			_dictLocalRotations.Add (child, Quaternion.identity);
		}
	}
		
	//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	public void RecordObjectPose(float time)
	{
		foreach (Transform child in _objectToRecord.GetComponentsInChildren<Transform>())
		{
			if (!_dictTransformToAnimCurve.ContainsKey(child))
				continue;
			float localDiff = Vector3.Distance (child.localPosition, _dictLocalPositions [child]);
			if (localDiff > 0f)
			{
				_dictLocalPositions [child] = child.localPosition;
				for (int i = 0; i < 3; i++)
					_dictTransformToAnimCurve [child] [i].AddKey (new Keyframe (time, child.localPosition [i]));
			}
			float globalDiff = Vector3.Distance (child.position, _dictWorldPositions [child]);
			if (globalDiff > 0f)
			{
				_dictWorldPositions [child] = child.position;
				for (int i = 0; i < 4; i++)
					_dictTransformToAnimCurve [child] [3 + i].AddKey (new Keyframe (time, child.localRotation [i]));
			}
			else 
			{
				for (int i = 0; i < 4; i++)
					_dictTransformToAnimCurve [child] [3 + i].AddKey (new Keyframe (time, _dictLocalRotations [child] [i]));
			}
			_dictLocalRotations [child] = child.localRotation;
		}
	}

	//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	public void SaveOutputToClip(string clipPath, Dictionary<Transform, Transform> switchCurves)
	{
		AnimationClip clip = new AnimationClip();
		foreach (Transform child in _objectToRecord.GetComponentsInChildren<Transform>())
		{
			if (!_dictTransformToAnimCurve.ContainsKey(child))
				continue;
			for(int i = 0; i < 7; i++)
			{
				int numberOfKeys = _dictTransformToAnimCurve [child] [i].length;
				for (int j = 0; j < numberOfKeys; j++)
				{
					AnimationUtility.SetKeyLeftTangentMode (_dictTransformToAnimCurve [child] [i], j, AnimationUtility.TangentMode.ClampedAuto);
					AnimationUtility.SetKeyRightTangentMode (_dictTransformToAnimCurve [child] [i], j, AnimationUtility.TangentMode.ClampedAuto);
				}
			}
			Transform curveTransform = child;
			if (switchCurves != null && switchCurves.ContainsKey (child))
				curveTransform = switchCurves [child];
			if (curveTransform == null)
				continue;
			string relativePath = curveTransform.name;
			if (curveTransform == _objectToRecord)
			{
				relativePath = "";
			} 
			else 
			{
				Transform currentTransform = curveTransform;
				while (currentTransform.parent != null && currentTransform.parent != _objectToRecord) 
				{
					relativePath = currentTransform.parent.name + "/" + relativePath;
					currentTransform = currentTransform.parent;
				}
				if (currentTransform.parent == null)
					continue;
			}
			string[] correspondences = new string[] {
				"localPosition.x",
				"localPosition.y",
				"localPosition.z",
				"localRotation.x",
				"localRotation.y",
				"localRotation.z",
				"localRotation.w"
			};
			for (int i = 0; i < 7; i++)
				if (_dictTransformToAnimCurve [child] [i].length > 1)
					clip.SetCurve (relativePath, typeof(Transform), correspondences [i], _dictTransformToAnimCurve [child] [i]);
		}
		clip.EnsureQuaternionContinuity ();
		AssetDatabase.CreateAsset(clip, clipPath);
	}



}
