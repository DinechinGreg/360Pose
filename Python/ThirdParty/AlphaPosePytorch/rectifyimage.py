import numpy as np
import cv2

def cart2sph(points):
    rho = np.linalg.norm(points, axis=1)
    phi = np.zeros(len(rho))
    indices = np.where(rho != 0)
    phi[indices] = np.arccos(points[indices][:,1] / rho[indices])
    theta = (np.arctan2(points[:,0],points[:,2]) + 2 * np.pi) % (2 * np.pi)
    return rho, theta, phi

def proj(points, width, height) :
    rho, theta, phi = cart2sph(points)
    pixelX = ((2 * np.pi - theta) * (width - 1) / (2 * np.pi))
    pixelY = (phi * (height - 1) / np.pi)
    pixel = np.column_stack([pixelY, pixelX])
    return pixel

def createRotMatrix(axis, theta):
    axis = axis/np.linalg.norm(axis)
    a = np.cos(theta/2.0)
    b, c, d = -axis*np.sin(theta/2.0)
    return np.array([[a*a+b*b-c*c-d*d, 2*(b*c-a*d), 2*(b*d+a*c)],
                  [2*(b*c+a*d), a*a+c*c-b*b-d*d, 2*(c*d-a*b)],
                  [2*(b*d-a*c), 2*(c*d+a*b), a*a+d*d-b*b-c*c]])

def rotate(points, R) :
    return np.dot(R, points.T).T

def rotateAround(points, R, center) :
    return rotate(points - center, R) + center

def rectifyAround(inputImg, box, dim) :
    personHeight = 175 * (dim / 224)
    margin = np.array([(box[3] - box[1])/10, (box[2] - box[0])/10])
    bounds = box + np.array([-margin[1], -margin[0], margin[1], margin[0]])
    inputHeight, inputWidth, _ = inputImg.shape 
    center = np.array([(bounds[3] + bounds[1])/2, (bounds[2] + bounds[0])/2])
    maxDiff = max(bounds[3] - bounds[1], bounds[2] - bounds[0])
    fov = (2 * np.pi) * maxDiff / (inputWidth - 1.0)
    d = personHeight / (2 * np.tan(fov/2.0))
    rng = dim/2 - np.arange(dim)
    xv, yv = np.meshgrid(rng, rng)
    grid3D = np.column_stack((xv.reshape(-1), 
                            yv.reshape(-1),
                            d * np.ones(dim*dim)
                           )).reshape(dim*dim, 3)
    rotTheta = (2 * np.pi) * (center[1] / (inputWidth - 1.0))
    rotPhi = np.pi * (0.5 - center[0] / (inputHeight - 1))
    RTheta = createRotMatrix(np.array([0,1,0]), rotTheta)
    grid3D = rotateAround(grid3D, RTheta, np.zeros(3))
    rotPhiAxis = rotateAround(np.array([1,0,0]), RTheta, np.zeros(3))
    RPhi = createRotMatrix(rotPhiAxis, rotPhi)
    grid3D = rotateAround(grid3D, RPhi, np.zeros(3))
    projectedPixels = proj(grid3D, inputWidth, inputHeight).astype(int)
    outImg = inputImg[projectedPixels[:,0], projectedPixels[:,1]]
    outImg = outImg.reshape(dim, dim, 3) 
    #tempImg = cv2.GaussianBlur(outImg, (5,5), 5)
    #outImg = cv2.addWeighted(outImg, 1.5, tempImg, -0.5, 0)
    return outImg, (d, rotTheta, rotPhi)
