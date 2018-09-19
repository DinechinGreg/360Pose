"""
Demo of HMR.

Note that HMR requires the bounding box of the person in the image. The best performance is obtained when max length of the person in the image is roughly 150px. 

When only the image path is supplied, it assumes that the image is centered on a person whose length is roughly 150px.
Alternatively, you can supply output of the openpose to figure out the bbox and the right scale factor.

Sample usage:

# On images on a tightly cropped image around the person
python -m demo --img_path data/im1963.jpg
python -m demo --img_path data/coco1.png

# On images, with openpose output
python -m demo --img_path data/random.jpg --json_path data/random_keypoints.json
"""
from __future__ import absolute_import
from __future__ import division
from __future__ import print_function

import sys
from absl import flags
import numpy as np

import skimage.io as io
import tensorflow as tf

from src.util import renderer as vis_util
from src.util import image as img_util
from src.util import openpose as op_util
import src.config
from src.RunModel import RunModel
from src.tf_smpl.batch_lbs import batch_rodrigues

import skvideo.io
import matplotlib.pyplot as plt
from mpl_toolkits.mplot3d import Axes3D
import json

flags.DEFINE_string('img_path', 'data/im1963.jpg', 'Image to run')
flags.DEFINE_string(
    'json_path', None,
    'If specified, uses the openpose output to crop the image.')

###
def cart2sph(points):
    rho = np.linalg.norm(points, axis=1)
    phi = np.zeros(len(rho))
    indices = np.where(rho != 0)
    phi[indices] = np.arccos(points[indices][:,1] / rho[indices])
    theta = (np.arctan2(points[:,0],points[:,2]) + 2 * np.pi) % (2 * np.pi)
    return rho, theta, phi

###
def omniprojection(points, width, height) :
    rho, theta, phi = cart2sph(points)
    pixelX = ((2 * np.pi - theta) * (width - 1) / (2 * np.pi))
    pixelY = (phi * (height - 1) / np.pi)
    pixel = np.column_stack([pixelY, pixelX])
    return pixel

###
def createRotMatrix(axis, theta):
    axis = axis/np.linalg.norm(axis)
    a = np.cos(theta/2.0)
    b, c, d = -axis*np.sin(theta/2.0)
    return np.array([[a*a+b*b-c*c-d*d, 2*(b*c-a*d), 2*(b*d+a*c)],
                  [2*(b*c+a*d), a*a+c*c-b*b-d*d, 2*(c*d-a*b)],
                  [2*(b*d-a*c), 2*(c*d+a*b), a*a+d*d-b*b-c*c]])

###
def rotate(points, R) :
    return np.dot(R, points.T).T

###
def combineRotations(firstR, secondR) :
    return np.dot(secondR, firstR);

###
def prepare_visualization() :
    plt.ion()
    fig2D1 = plt.figure(1)
    ax2D1 = fig2D1.add_subplot(121)
    ax2D1.axis('off')
    ax2D2 = fig2D1.add_subplot(122)
    ax2D2.axis('off')
    fig2D2 = plt.figure(2)
    ax2D3 = fig2D2.add_subplot(111)
    ax2D3.axis('off')
    fig3D = plt.figure(3)
    ax3D = plt.subplot(111, projection='3d')
    def press(event):
        if event.key == 'q' :
            sys.exit()
    fig2D1.canvas.mpl_connect('key_press_event', press)
    fig2D2.canvas.mpl_connect('key_press_event', press)
    fig3D.canvas.mpl_connect('key_press_event', press)
    axs = (ax2D1, ax2D2, ax2D3, ax3D)
    return axs

###
def scale3Dplot(ax3D, verts, degreesrot) :
    ax3D.view_init(elev=-degreesrot[2], azim=180-degreesrot[1])
    mins = np.min(verts, axis=0)
    maxs = np.max(verts, axis=0)
    center = (mins + maxs)[[2,0,1]] / 2
    r = max(np.abs((maxs - mins) / 2))
    return np.column_stack((center - r, center + r))

###
def visualize(img, omniimg, cam_for_render, vert_shifted, omni_vert, joints_orig, axs, ax3Dscale, wait_time) :
    proj = vis_util.ProjectPoints(
            v = vert_shifted,
            f=cam_for_render[0] * np.ones(2),
            rt=np.zeros(3),
            t=np.zeros(3),
            k=np.zeros(5),
            c=cam_for_render[1:3])[:,[1,0]]
    skel_img = vis_util.draw_skeleton(img, joints_orig)
    axs[0].imshow(img)
    axs[1].imshow(skel_img)
    axs[2].cla()
    omniproj = omniprojection(omni_vert, omniimg.shape[1], omniimg.shape[0])
    temp = np.array(proj).astype(int)
    cols = img[temp[:,0], temp[:,1]] / 255.0
    axs[2].imshow(img)
    axs[2].scatter(proj[:,1], proj[:,0], s=0.1)
    axs[3].cla()
    axs[3].scatter(omni_vert[:,2], omni_vert[:,0], omni_vert[:,1], s=0.5, c=cols)
    axs[3].auto_scale_xyz(*ax3Dscale)
    axs[3].set_xlabel('z')
    axs[3].set_ylabel('x')
    axs[3].set_zlabel('y')
    plt.draw()
    plt.show()
    plt.pause(wait_time)

##
def quaternions_from_matrices(rotMatrices) :
    zeroArray = np.zeros(len(rotMatrices))
    x = np.sqrt(np.max((zeroArray, 1 + rotMatrices[:,0,0] - rotMatrices[:,1,1] - rotMatrices[:,2,2]), axis=0)) / 2
    x *= np.sign(x * (rotMatrices[:,2,1] - rotMatrices[:,1,2]))
    y = np.sqrt(np.max((zeroArray, 1 - rotMatrices[:,0,0] + rotMatrices[:,1,1] - rotMatrices[:,2,2]), axis=0)) / 2
    y *= np.sign(y * (rotMatrices[:,0,2] - rotMatrices[:,2,0]))
    z = np.sqrt(np.max((zeroArray, 1 - rotMatrices[:,0,0] - rotMatrices[:,1,1] + rotMatrices[:,2,2]), axis=0)) / 2
    z *= np.sign(z * (rotMatrices[:,1,0] - rotMatrices[:,0,1]))
    w = np.sqrt(np.max((zeroArray, 1 + rotMatrices[:,0,0] + rotMatrices[:,1,1] + rotMatrices[:,2,2]), axis=0)) / 2
    x *= -1
    w *= -1
    return np.column_stack((x,y,z,w))

###
def save_poseshape(sess, outJSONPath, theta, pelvis, totalR) :
    poseshape = {}
    poseshape['translation'] = (pelvis * np.array([-1,1,1])).tolist()
    Rs = tf.reshape(batch_rodrigues(tf.reshape(theta[3:-10], [-1, 3])), [-1, 24, 3, 3])
    numpyRs = Rs.eval(session=sess)[0]
    numpyRs[0] = combineRotations(numpyRs[0], totalR)
    quaternions = quaternions_from_matrices(numpyRs)
    poseshape['pose'] = quaternions.tolist()
    poseshape['shape'] = theta[-10:].tolist()
    with open(outJSONPath, 'w') as f:
        json.dump(poseshape, f)

###
def preprocess_image(img, json_path=None):
    if img.shape[2] == 4:
        img = img[:, :, :3]
    if json_path is None:
        if np.max(img.shape[:2]) != config.img_size:
            print('Resizing so the max image size is %d..' % config.img_size)
            scale = (float(config.img_size) / np.max(img.shape[:2]))
        else:
            scale = 1.
        center = np.round(np.array(img.shape[:2]) / 2).astype(int)
        center = center[::-1]
    else:
        scale, center = op_util.get_bbox(json_path)
    crop, proc_param = img_util.scale_and_crop(img, scale, center,
                                               config.img_size)
    crop = 2 * ((crop / 255.) - 0.5)
    return crop, proc_param, img

###
def getRotForFrame(video_folder, frame_number) :
    with open(video_folder + 'ThirdParty/AlphaPosePytorch/results/sep-json/' + str(frame_number) + '.json') as f:
        data = json.load(f)
        rot = data['camera_rot']
        return rot

###
def process_image(sess, outJSONPath, img, model, rot, json_path=None) :
    input_img, proc_param, img = preprocess_image(img, json_path)
    input_img = np.expand_dims(input_img, 0)
    joints, verts, cams, joints3d, theta = model.predict(
        input_img, get_theta=True)
    joints, verts, theta, cam = joints[0], verts[0], theta[0], cams[0]
    img_size = proc_param['img_size']
    undo_scale = 1. / np.array(proc_param['scale'])
    cam_s = cam[0]
    cam_pos = cam[1:]
    principal_pt = np.array([img_size, img_size]) / 2.
    flength = rot[0]
    tz = flength / (0.5 * img_size * cam_s * undo_scale)
    trans = np.hstack([cam_pos, tz])
    vert_shifted = verts + trans
    RAxisSwitch = createRotMatrix(np.array([0,0,1]), np.pi)
    RTheta = createRotMatrix(np.array([0,1,0]), rot[1])
    rotPhiAxis = rotate(np.array([1,0,0]), RTheta)
    RPhi = createRotMatrix(rotPhiAxis, rot[2])
    RCombined = combineRotations(RAxisSwitch, combineRotations(RTheta, RPhi))
    omni_vert = rotate(vert_shifted, RCombined)
    joints_shifted = joints3d[0] + trans
    omni_joints = rotate(joints_shifted, RCombined)
    omni_pelvis = (omni_joints[2] + omni_joints[3])/2
    start_pt = proc_param['start_pt'] - 0.5 * img_size
    final_principal_pt = (principal_pt + start_pt) * undo_scale
    cam_for_render = np.hstack([np.mean(flength), final_principal_pt])
    margin = int(img_size / 2)
    joints_orig = (joints + proc_param['start_pt'] - margin) * undo_scale
    if outJSONPath != None :
        save_poseshape(sess, outJSONPath, theta, omni_pelvis, RCombined)
    return cam_for_render, vert_shifted, omni_vert, joints_orig

###
def main(img_path, is_video, json_path=None):
    sess = tf.Session()
    model = RunModel(config, sess=sess)

    #axs = prepare_visualization()

    if is_video :
        video_folder = '../../'
        omni_video_path = video_folder + 'inputVideo.mp4'
        omnivideogen = skvideo.io.vreader(omni_video_path)
        for frame in omnivideogen :
            omniimg = frame
            break
        video_path = video_folder + 'ThirdParty/AlphaPosePytorch/results/AlphaPose_extractedFramesVideo.avi'
        frame_number = 0
        videogen = skvideo.io.vreader(video_path)
        for frame in videogen :
            outJSONPath = 'results/' + str(frame_number) + '.json'
            rot = getRotForFrame(video_folder, frame_number)
            cam_for_render, vert_shifted, omni_vert, joints_orig = process_image(sess, outJSONPath, frame, model, rot, json_path)
            #if frame_number==0 :
            #    ax3Dscale = scale3Dplot(axs[3], omni_vert, np.degrees(rot))
            #visualize(frame, omniimg, cam_for_render, vert_shifted, omni_vert, joints_orig, axs, ax3Dscale, 1)
            print('Finished processing frame %d' % frame_number)
            frame_number += 1
        return    

    img = io.imread(img_path)
    cam_for_render, vert_shifted, omni_vert, joints_orig = process_image(sess, None, img, model, (500,0,0), json_path)
    ax3Dscale = scale3Dplot(axs[3], vert_shifted, (0,0,0))
    visualize(img, img, cam_for_render, vert_shifted, vert_shifted, joints_orig, axs, ax3Dscale, 1000)

###
if __name__ == '__main__':
    config = flags.FLAGS
    config(sys.argv)
    config.load_path = src.config.PRETRAINED_MODEL
    config.batch_size = 1
    is_video = False
    if config.img_path=='video' :
        is_video = True
    main(config.img_path, is_video, config.json_path)
