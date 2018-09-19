. /media/greg/Data/anaconda2/etc/profile.d/conda.sh
conda activate py2env-tensorflow
cd ThirdParty/hmr
rm -r results/*
python -m demo --img_path video
