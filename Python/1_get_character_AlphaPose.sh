. /media/greg/Data/anaconda2/etc/profile.d/conda.sh
conda activate py3env-pytorch
cd ThirdParty/AlphaPosePytorch
python3 video_demo.py --video results/extractedFramesVideo.mp4 --outdir results --save_img --save_video --format cmu
rm -r ../densepose/input_data/*
mv results/vis/* ../densepose/input_data/
