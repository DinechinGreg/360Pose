using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayAnimation : MonoBehaviour 
{
	private Transform _headTransform;
	private Animation _anim;
	private AudioSource _audioSource;

	//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	IEnumerator Start () 
	{
		yield return StartCoroutine (CreateSphereCoroutine ());
		CreateAvatar ();
		StartCoroutine (LoopAnimationCoroutine ());
	}

	//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	void LateUpdate()
	{
		if (_headTransform != null)
			_headTransform.LookAt (Camera.main.transform.position - 0.1f * _headTransform.up);
	}

	//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	private IEnumerator CreateSphereCoroutine()
	{
		Texture2D backgroundCol = Resources.Load<Texture2D> ("Background");
		Material mat = new Material (Shader.Find ("Custom/BackgroundDisplaySphereShader"));
		mat.SetTexture ("_MainTex", backgroundCol);
		GameObject deformableSphereGO = new GameObject ("Background");
		DeformableSphere deformableSphere = deformableSphereGO.AddComponent<DeformableSphere> ();
		deformableSphere.Generate (backgroundCol.width, backgroundCol.height/2, 10f, mat, null);
		while (deformableSphere._isCreatingMesh)
			yield return null;
		deformableSphereGO.transform.RotateAround (Vector3.zero, Vector3.up, -90f);
	}

	//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	private Transform FindRecursiveChild(Transform parent, string childName)
	{
		foreach (Transform child in parent.GetComponentsInChildren<Transform>())
		{
			if (child.name == childName) 
			{
				return child;
			}
		}
		return null;
	}

	//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	private void CreateAvatar()
	{
		GameObject avatarPrefab = Resources.Load<GameObject> ("Prefabs/AnimatableAvatarPrefab");
		Transform avatarTransform = GameObject.Instantiate (avatarPrefab).transform;
		_headTransform = FindRecursiveChild(avatarTransform, "m_avg_Neck");
		_anim = avatarTransform.gameObject.AddComponent<Animation> ();
		AnimationClip animClip = Resources.Load<AnimationClip> ("Animation");
		_anim.AddClip (animClip, "Animation");
		_audioSource = _headTransform.gameObject.AddComponent<AudioSource> ();
		AudioClip audioClip = Resources.Load<AudioClip> ("audio");
		_audioSource.clip = audioClip;
	}

	//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	private IEnumerator LoopAnimationCoroutine()
	{
		_anim.Play ("Animation");
		_audioSource.Play ();
		yield return null;
		while (_anim.isPlaying || _audioSource.isPlaying)
			yield return null;
		StartCoroutine (LoopAnimationCoroutine ());
	}
}
