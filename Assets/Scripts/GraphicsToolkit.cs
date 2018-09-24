using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class GraphicsToolkit : MonoBehaviour 
{
	//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	public static void CreateTextureFromRenderTexture(ref Texture2D dest, RenderTexture src)
	{
		dest = new Texture2D (src.width, src.height);
		dest.filterMode = src.filterMode;
		RenderTexture.active = src;
		dest.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
		dest.Apply();
		RenderTexture.active = null;
	}

	//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	public static void SaveRenderTextureToFile(RenderTexture renderTexture, string filePath)
	{
		Texture2D output = new Texture2D (1, 1);
		CreateTextureFromRenderTexture (ref output, renderTexture);		
		File.WriteAllBytes(filePath, output.EncodeToPNG());
		Texture2D.DestroyImmediate (output);
	}

	//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	public static Color DepthToRGB8(float depth)
	{
		Vector3 analog255RGB = (Mathf.Pow (2, 8 - Mathf.Min (depth, 16) / 2) - 1) * Vector3.one;
		Color colRGB = new Color (analog255RGB.x / 255f, analog255RGB.y / 255f, analog255RGB.z / 255f);
		return colRGB;
	}

	//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	public static float RGBToDepth8 (Color colRGB)
	{
		float depth = 16 - 2 * Mathf.Log (255 * colRGB.r + 1) / Mathf.Log (2);
		return depth;
	}

	//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	public static Color DepthToRGB16(float depth)
	{
		int intDepth = Mathf.RoundToInt (4000 * Mathf.Min(depth, Mathf.Pow(2,16)-1));
		int r = intDepth / 256;
		int g = intDepth % 256;
		if (r % 2 == 1)
			g = 255 - g;
		Vector3 analog255RGB = new Vector3 (r, g, 128);
		Color colRGB = new Color (analog255RGB.x / 255f, analog255RGB.y / 255f, analog255RGB.z / 255f);
		return colRGB;
	}

	//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	public static float RGBToDepth16 (Color colRGB)
	{
		int r = Mathf.RoundToInt (255 * colRGB.r);
		int g = Mathf.RoundToInt (255 * colRGB.g);
		if (r % 2 == 1)
			g = 255 - g;
		int intDepth = 256 * r + g;
		float depth = intDepth / 4000f;
		return depth;
	}

	//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	public static Color DepthToRGB(float depth)
	{
		return DepthToRGB16 (depth);
	}

	//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	public static float RGBToDepth (Color colRGB)
	{
		return RGBToDepth16 (colRGB);
	}
}
