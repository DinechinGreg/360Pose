using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeformableSphere : MonoBehaviour
{
	public bool _isCreatingMesh = false;
	public Material _material;

	private int _textureWidth;
	private int _textureHeight;
	private float _radius;
	private Vector3[] _vertices;
	private Vector2[] _uvs;
	private Vector3[] _normals;
	private int[] _triangles;

	//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	public void Generate(int width, int height, float radius, Material mat, Texture2D depthTexture = null)
	{
		_isCreatingMesh = true;
		_textureWidth = width;
		_textureHeight = height;
		_radius = radius;
		_material = mat;

		CalculateVerticesUVsNormals (depthTexture);
		CalculateTriangles ();

		StartCoroutine(GenerateMeshCoroutine ("Mesh", 2 * _textureWidth, depthTexture != null));
	}

	//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	private void CalculateVerticesUVsNormals(Texture2D depthTexture = null)
	{
		int numberOfVertices = (_textureWidth + 1) * _textureHeight + 2;
		_vertices = new Vector3[numberOfVertices];
		_uvs = new Vector2[numberOfVertices];
		_normals = new Vector3[numberOfVertices];
		float _pi = Mathf.PI;
		float _2pi = _pi * 2f;

		_vertices[0] = Vector3.up * _radius;
		_uvs [0] = Vector2.up;
		_normals [0] = Vector3.up;

		for(int v = 0; v < _textureHeight; v++)
		{
			float a1 = _pi * (float)(v + 1) / (_textureHeight + 1);
			float sin1 = Mathf.Sin(a1);
			float cos1 = Mathf.Cos(a1);
			for( int u = 0; u <= _textureWidth; u++ )
			{
				float a2 = _2pi * (float)(u == _textureWidth ? 0 : u) / _textureWidth;
				float sin2 = Mathf.Sin(a2);
				float cos2 = Mathf.Cos(a2);

				int index = u + v * (_textureWidth + 1) + 1;
				_normals [index] = new Vector3 (sin1 * cos2, cos1, sin1 * sin2);
				_vertices[index] = _normals [index] * _radius;
				if (depthTexture != null) 
				{
					Color colRGB = depthTexture.GetPixel (_textureWidth - 1 - u, _textureHeight - 1 - v);
					float depth = GraphicsToolkit.RGBToDepth(colRGB);
					_vertices[index] = _normals [index] * depth;
				}
				_uvs [index] = new Vector2 (1f - (float)u / _textureWidth, 1f - (float)(v + 1) / (_textureHeight + 1));
			}
		}

		_vertices [numberOfVertices - 1] = -Vector3.up * _radius;
		_uvs[numberOfVertices-1] = Vector2.zero;
		_normals [numberOfVertices-1] = -Vector3.up;
	}


	//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	private void CalculateTriangles()
	{
		int nbFaces = _vertices.Length;
		int nbTriangles = nbFaces * 2;
		int nbIndexes = nbTriangles * 3;
		_triangles = new int[nbIndexes];

		int i = 0;
		for(int u = 0; u < _textureWidth; u++)
		{
			_triangles [i++] = u + 2;
			_triangles [i++] = 0;
			_triangles [i++] = u + 1;
		}
		for( int v = 0; v < _textureHeight - 1; v++)
		{
			for( int u = 0; u < _textureWidth; u++)
			{
				int current = u + v * (_textureWidth + 1) + 1;
				int next = current + _textureWidth + 1;

				_triangles [i++] = current;
				_triangles [i++] = next + 1;
				_triangles [i++] = current + 1;

				_triangles [i++] = current;
				_triangles [i++] = next;
				_triangles [i++] = next + 1;
			}
		}
		for(int u = 0; u < _textureWidth; u++)
		{
			_triangles [i++] = _vertices.Length - 1;
			_triangles [i++] = _vertices.Length - (u + 1) - 1;
			_triangles [i++] = _vertices.Length - (u + 2) - 1;
		}
	}

	//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	private IEnumerator GenerateMeshCoroutine(string prefix, int safetyMargin, bool generateColliders = false)
	{
		int leftToProcess = _vertices.Length;
		int meshNumber = 0;
		int maxNumberOfVertices = 65000;
		while (leftToProcess + meshNumber * safetyMargin > 0) 
		{
			GameObject meshObject = new GameObject (prefix + meshNumber.ToString());
			meshObject.transform.parent = transform;
			meshObject.transform.localPosition = Vector3.zero;
			meshObject.transform.localRotation = Quaternion.identity;

			MeshFilter filter = meshObject.AddComponent< MeshFilter >();
			Mesh mesh = new Mesh ();
			mesh.name = meshObject.name;
			mesh.Clear ();
			filter.mesh = mesh;

			int numberOfVertices = Mathf.Min (maxNumberOfVertices, leftToProcess + meshNumber * safetyMargin);
			List<Vector3> decimatedVertices = new List<Vector3> ();
			for (int k = 0; k < numberOfVertices; k++)
				decimatedVertices.Add (_vertices [k + meshNumber * (maxNumberOfVertices - safetyMargin)]);
			mesh.vertices = decimatedVertices.ToArray();

			int numberOfUVs = numberOfVertices;
			List<Vector2> decimatedUVs = new List<Vector2> ();
			for (int k = 0; k < numberOfUVs; k++)
				decimatedUVs.Add (_uvs [k + meshNumber * (maxNumberOfVertices - safetyMargin)]);
			mesh.uv = decimatedUVs.ToArray ();

			int numberOfNormals = numberOfVertices;
			List<Vector3> decimatedNormals = new List<Vector3> ();
			for (int k = 0; k < numberOfNormals; k++)
				decimatedNormals.Add (_normals [k + meshNumber * (maxNumberOfVertices - safetyMargin)]);
			mesh.normals = decimatedNormals.ToArray ();

			int minVertex = meshNumber * (maxNumberOfVertices - safetyMargin);
			int maxVertex = minVertex + numberOfVertices;
			List<int> decimatedTriangles = new List<int> ();
			for (int k = 0; k < _triangles.Length; k+= 3) 
			{
				int firstIndex = _triangles [k];
				int secondIndex = _triangles [k + 1];
				int thirdIndex = _triangles [k + 2];
				if (firstIndex >= minVertex && firstIndex < maxVertex && secondIndex >= minVertex && secondIndex < maxVertex && thirdIndex >= minVertex && thirdIndex < maxVertex) 
				{
					decimatedTriangles.Add (firstIndex - meshNumber * (maxNumberOfVertices - safetyMargin));
					decimatedTriangles.Add (secondIndex - meshNumber * (maxNumberOfVertices - safetyMargin));
					decimatedTriangles.Add (thirdIndex - meshNumber * (maxNumberOfVertices - safetyMargin));
				}
			}
			mesh.triangles = decimatedTriangles.ToArray ();

			mesh.RecalculateBounds();

			MeshRenderer renderer = meshObject.AddComponent<MeshRenderer> ();
			renderer.material = _material;
			renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

			if (generateColliders) 
			{
				meshObject.AddComponent<MeshCollider> ();
			}

			Debug.Log ("Finished creating mesh " + meshNumber.ToString() + ".");
			meshNumber++;
			leftToProcess -= maxNumberOfVertices;
			yield return null;
		}
		Debug.Log ("Finished creating mesh.");
		_isCreatingMesh = false;
		StopAllCoroutines ();
	}
}