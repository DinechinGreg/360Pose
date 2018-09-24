import numpy
import cv2
import matplotlib.pyplot as plt
import numpy as np
from scipy.io import loadmat
import scipy
import scipy.misc
import scipy.cluster
import os

_atlasDim = 255
_numberOfFrames = int(len([name for name in os.listdir('./results/')])/3)

def DrawOnAtlas(atlas, tex, minV, maxV, minU, maxU, v, u) :
    tempV = minV + ((255-v)*((maxV-minV-1)/255.0)).astype(int)
    tempU = minU + (u*((maxU-minU-1)/255.0)).astype(int)
    atlas[tempV, tempU] = tex

def GrowTextureIntoZeros(tex, kernelVal, iters) :
    for i in range(iters) :
        indicesWhereNotZero = np.where(np.sum(tex, axis=2) != 0)
        nonZeroMask = np.zeros(tex.shape[:2], dtype=np.bool)
        nonZeroMask[indicesWhereNotZero] = True
        alphaMask = np.zeros(tex.shape)
        alphaMask[np.where(nonZeroMask==True)] = 1
        alphaMask = cv2.GaussianBlur(alphaMask,(kernelVal,kernelVal),0)
        blurred = cv2.GaussianBlur(tex,(kernelVal,kernelVal),0)
        indicesWhereBlurred = np.where(alphaMask != 0)
        blurred[indicesWhereBlurred] *= 1.0/alphaMask[indicesWhereBlurred]
        indicesToWrite = np.where(nonZeroMask==False)
        tex[indicesToWrite] = blurred[indicesToWrite]

def ExpandVoronoi(atlas, margin) :
    for i in range(4) :
        for j in range(6) :
            tex = atlas[_atlasDim*j:_atlasDim*(j+1),_atlasDim*i:_atlasDim*(i+1),:]
            height, width, _ = tex.shape
            indicesWhereZero = np.where(np.sum(tex, axis=2) == 0)
            alphaMask = np.zeros((height, width)).astype(np.uint8)
            alphaMask[indicesWhereZero] = 1
            distance = cv2.distanceTransform(alphaMask, cv2.DIST_L2, cv2.DIST_MASK_PRECISE ) + margin
            grad = np.array(np.gradient(distance))
            gradLength = np.sqrt(grad[0]**2 + grad[1]**2)
            indicesGradLength = np.where(gradLength != 0)
            deltaY, deltaX = -distance*grad
            deltaY[indicesGradLength] /= gradLength[indicesGradLength]
            deltaX[indicesGradLength] /= gradLength[indicesGradLength]
            yArray = ((np.arange(0,height) * np.ones((height, width)).T).T + deltaY) % height
            xArray = (np.arange(0,width) * np.ones((height, width)) + deltaX) % width
            drawTex = tex[yArray.astype(int), xArray.astype(int)]
            drawTex = cv2.blur(drawTex, (11,11), 0)
            atlas[_atlasDim*j:_atlasDim*(j+1),_atlasDim*i:_atlasDim*(i+1),:] = drawTex
    return atlas

def GetDominantColor(tex) :
    indicesWhereNonZero = np.where(np.sum(tex, axis=2) != 0)
    nonZeroTex = tex[indicesWhereNonZero[0], indicesWhereNonZero[1], :]
    codes, dist = scipy.cluster.vq.kmeans(nonZeroTex, 10)
    vecs, dist = scipy.cluster.vq.vq(nonZeroTex, codes)
    counts, bins = scipy.histogram(vecs, len(codes))
    dominantColor = codes[np.argmax(counts)]
    return dominantColor

def FillWithDominantColor(atlas) :
    outTex = atlas.copy()
    groupedParts = np.array([[1,2],[3,4],[5,6],[7,9],[8,10],[11,13],[12,14],
                             [15,17],[16,18],[19,21],[20,22],[23,24]])
    for partGroup in groupedParts :
        tex = np.zeros((_atlasDim, _atlasDim, 3))
        indicesY, indicesX = np.array([]), np.array([])
        for partIndex in partGroup :
            i, j = int((partIndex-1)/6), int((partIndex-1)%6)
            temp = atlas[_atlasDim*j:_atlasDim*(j+1),_atlasDim*i:_atlasDim*(i+1),:]
            indicesWhereZero = np.where(np.sum(temp, axis=2) == 0)
            indicesY = np.concatenate((indicesY, _atlasDim*j+indicesWhereZero[0]))
            indicesX = np.concatenate((indicesX, _atlasDim*i+indicesWhereZero[1]))
            tex = np.hstack((tex, temp))
        dominantCol = GetDominantColor(tex)
        outTex[indicesY.astype(int), indicesX.astype(int)] = dominantCol
    return outTex

def GetTextureAtlasFrom(texIm, IUV) :
    U = IUV[:,:,1]
    V = IUV[:,:,2]
    atlas = np.zeros((6*_atlasDim,4*_atlasDim,3))
    for i in range(4) :
        for j in range(6) :
            pixelsForPart = np.where(IUV[:,:,0]==6*i+j+1)
            if len(pixelsForPart[0]) > 0 :
                drawTex = (texIm[:,:,::-1]/255.)[pixelsForPart[0],pixelsForPart[1]] 
                texU = 255*(1-U[pixelsForPart])
                texV = 255*(1-V[pixelsForPart])
                DrawOnAtlas(atlas, drawTex, _atlasDim*j, _atlasDim*(j+1), _atlasDim*i, _atlasDim*(i+1), texV, texU)
    return atlas

def DrawBaseAtlas() :
    ALP_UV = loadmat('UV_data/UV_Processed.mat')
    FaceIndices = np.array( ALP_UV['All_FaceIndices']).squeeze()
    FacesDensePose = ALP_UV['All_Faces']-1
    U_norm = ALP_UV['All_U_norm'].squeeze()
    V_norm = ALP_UV['All_V_norm'].squeeze()
    atlas = np.zeros((6*_atlasDim,4*_atlasDim,3))
    for i in range(4) :
        for j in range(6) :
            FaceIndicesNow = np.where(FaceIndices == 6*i+j+1)
            FacesNow = FacesDensePose[FaceIndicesNow]
            texU = (255*U_norm[FacesNow].flatten()).astype(int)
            texV = (255*V_norm[FacesNow].flatten()).astype(int)
            DrawOnAtlas(atlas, 1, _atlasDim*j, _atlasDim*(j+1), _atlasDim*i, _atlasDim*(i+1), texV, texU)
    plt.figure(); plt.imshow(atlas); plt.show()
    cv2.imwrite('../../../Assets/SMPL/Samples/Materials/base_atlas.png', 255.0 * atlas[:,:,::-1])

def PiecewiseGrowIntoZeros(atlas, kernelVal, iters) :
    for i in range(4) :
        for j in range(6) :
            tex = atlas[_atlasDim*j:_atlasDim*(j+1),_atlasDim*i:_atlasDim*(i+1),:]
            GrowTextureIntoZeros(tex, kernelVal, iters)
            atlas[_atlasDim*j:_atlasDim*(j+1),_atlasDim*i:_atlasDim*(i+1),:] = tex
    
def PiecewiseBlur(atlas) :
    for i in range(4) :
        for j in range(6) :
            tex = (255*atlas[_atlasDim*j:_atlasDim*(j+1),_atlasDim*i:_atlasDim*(i+1),:]).astype(np.uint8)
            tex = cv2.GaussianBlur(tex,(9,9),0)
            tex = cv2.medianBlur(tex,9)
            atlas[_atlasDim*j:_atlasDim*(j+1),_atlasDim*i:_atlasDim*(i+1),:] = tex / 255.0
    
def GetSummedTexture(frameCount) :
    alphaMask = np.zeros((6*_atlasDim,4*_atlasDim,3))
    for i in range(frameCount) :
        imgIndex = str(i)
        IUV = cv2.imread('results/' + imgIndex + '_IUV.png')
        img = cv2.imread('input_data/' + imgIndex + '.png')
        tempTex = GetTextureAtlasFrom(img, IUV)
        indicesWhereNotZero = np.where(np.sum(tempTex, axis=2) != 0)
        alphaMask[indicesWhereNotZero] += 1
        if i == 0 :
            outTex = tempTex
        else :
            outTex += tempTex
        print('Finished frame ' + str(i) + '.\r')
    alphaNotZero = np.where(alphaMask != 0)
    outTex[alphaNotZero] *= 1.0/alphaMask[alphaNotZero]
    uvMask = cv2.imread('../../../Assets/SMPL/Samples/Materials/UVMask.png')
    outTex[np.where(uvMask == 0)] = 0
    return outTex

if __name__ == "__main__":
    outTex = GetSummedTexture(_numberOfFrames)
    PiecewiseGrowIntoZeros(outTex, 3, 3)
    outTex = FillWithDominantColor(outTex)
    uvMask = cv2.imread('../../../Assets/SMPL/Samples/Materials/UVMask.png')
    outTex[np.where(uvMask == 0)] = 0
    PiecewiseBlur(outTex)
    cv2.imwrite('../../../Assets/SMPL/Samples/Materials/demo_atlas.png', 255.0 * outTex[:,:,::-1])
    print('Wrote file to ../../../Assets/SMPL/Samples/Materials/demo_atlas.png.')
