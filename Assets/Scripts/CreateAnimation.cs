using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;
using SimpleJSON;

public class CreateAnimation : MonoBehaviour 
{
	public string _animationName;
	public AnimationClip _copyAnimationClip;
	public Vector2 _copyStartAndEndTime;
	public float _videoFPS;
	public int _videoFrameSkip;
	public int _videoNumberOfFrames;
	public GameObject _avatarFBX;
	public TextAsset _jointReg;

	private float[][] _translations;
	private float[][][] _poses;
	private Transform _avatarTransform;
	private SMPLBlendshapes _blendshapes;
	private AnimationRecorder _animRecorder;

	//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	IEnumerator Start () 
	{
		Initialize ();
		yield return null;
		if (_copyAnimationClip == null)
			yield return StartCoroutine (RecordFromVideoPoseCoroutine ());
		else
			yield return StartCoroutine (CopyFromAnimatorControllerCoroutine ());
		Debug.Log ("Finished!");
	}

	//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	private void Initialize()
	{
		_avatarTransform = GameObject.Instantiate (_avatarFBX).transform;
		_blendshapes = _avatarTransform.GetComponentInChildren<Renderer>().gameObject.AddComponent<SMPLBlendshapes> ();
		_blendshapes.jointRegressorJSON = _jointReg;
		_animRecorder = gameObject.AddComponent<AnimationRecorder> ();
		_animRecorder.Initialize (_avatarTransform);
	}

	//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	private void GetJSONInformation()
	{
		float[] averageShape = new float[10];
		_translations = new float[_videoNumberOfFrames][];
		_poses = new float[_videoNumberOfFrames][][];
		for(int frameIndex = 0; frameIndex < _videoNumberOfFrames; frameIndex++)
		{
			_translations[frameIndex] = new float[3];
			_poses[frameIndex] = new float[24][];
			using (StreamReader reader = new StreamReader (Application.dataPath + "/../Python/ThirdParty/hmr/results/" + frameIndex.ToString() + ".json"))
			{
				string text = reader.ReadToEnd ();
				reader.Close ();
				JSONNode node = JSON.Parse (text);
				for (int i = 0; i < 10; i++)
					averageShape [i] += node ["shape"] [i].AsFloat / _videoNumberOfFrames;
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
		using (StreamWriter writer = new StreamWriter (Application.dataPath + "/Resources/SMPL/SMPL_shape.json"))
		{
			writer.WriteLine ("{");
			string betas = "    \"betas\": [";
			foreach (float val in averageShape)
				betas += val.ToString () + ", ";
			betas = betas.Substring (0, betas.Length - 2) + "]";
			writer.WriteLine (betas);
			writer.WriteLine ("}");
			writer.Close ();
		}
	}

	//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	private IEnumerator SetAvatarPoseCoroutine(int frameIndex)
	{
		Transform armatureTransform = GameObject.Find ("Armature").transform;
		Transform pelvisTransform = GameObject.Find ("m_avg_Pelvis").transform;
		_avatarTransform.rotation = Quaternion.identity;
		armatureTransform.localPosition = Vector3.zero;
		armatureTransform.localRotation = Quaternion.identity;
		_blendshapes.getModifyBones ().ResetAll ();
		Vector3 basePelvisLocalPos = pelvisTransform.localPosition;
		_blendshapes.getModifyBones ().updateBoneAngles (_poses [frameIndex], new float[3]);
		armatureTransform.localPosition = new Vector3 (_translations [frameIndex] [0], _translations [frameIndex] [1], _translations [frameIndex] [2]) - basePelvisLocalPos;
		armatureTransform.localRotation = pelvisTransform.localRotation;
		pelvisTransform.localRotation = Quaternion.identity;
		yield return null;
	}

	//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	private IEnumerator RecordFromVideoPoseCoroutine()
	{
		GetJSONInformation ();
		for (int i = 0; i < _videoNumberOfFrames; i++) 
		{
			yield return StartCoroutine (SetAvatarPoseCoroutine (i));
			_animRecorder.RecordObjectPose(_videoFrameSkip*i/_videoFPS);
		}
		_animRecorder.SaveOutputToClip ("Assets/Resources/Animation/" + _animationName + ".anim", null);
	}

	//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	private IEnumerator CopyFromAnimatorControllerCoroutine()
	{
		Animator animator = _avatarTransform.GetComponent<Animator> ();
		AnimatorController animController = AnimatorController.CreateAnimatorControllerAtPath("Assets/Resources/Animation/CopyAnimatorController.controller");
		AnimatorStateMachine rootStateMachine = animController.layers[0].stateMachine;
		AnimatorState clipState = rootStateMachine.AddState ("Clip");
		clipState.motion = _copyAnimationClip;
		float animLength = _copyAnimationClip.length;
		float speed = 30;
		clipState.speed = speed;
		animator.runtimeAnimatorController = animController;
		float startTime = Time.time;
		float animTime = 0f;
		while((animTime = (Time.time - startTime) * speed) < animLength)
		{
			if (animTime > _copyStartAndEndTime [0] && animTime < _copyStartAndEndTime [1]) 
				_animRecorder.RecordObjectPose(animTime - _copyStartAndEndTime [0]);
			yield return null;
		}
		Dictionary<Transform, Transform> switchCurves = new Dictionary<Transform, Transform> ();
		switchCurves.Add (GameObject.Find ("Armature").transform, null);
		switchCurves.Add (_avatarTransform, GameObject.Find ("m_avg_root").transform);
		_animRecorder.SaveOutputToClip ("Assets/Resources/Animation/" + _animationName + ".anim", switchCurves);
	}
}

