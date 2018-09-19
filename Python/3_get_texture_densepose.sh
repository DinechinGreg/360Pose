cd ThirdParty/densepose
rm -r results/*
sudo service docker start
sudo nvidia-docker run --rm -v /media/greg/Data/ownCloud/Unity/360Pose/Python/ThirdParty/densepose:/denseposedata -it densepose:c2-cuda9-cudnn7-wdata \
python2 tools/infer_simple.py \
--cfg configs/DensePose_ResNet101_FPN_s1x-e2e.yaml \
--output-dir "DensePoseData/results/" \
--image-ext png \
--wts "DensePoseData/weights/DensePose_ResNet101_FPN_s1x-e2e.pkl" \
"DensePoseData/input_data/"
