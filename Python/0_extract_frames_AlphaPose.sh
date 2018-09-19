mkdir Temp
ffmpeg -y -i inputVideo.mp4 -vf "select=not(mod(n\,10))" -vsync vfr Temp/temp%03d.png
rm -r ThirdParty/AlphaPosePytorch/results/*
ffmpeg -y -i Temp/temp%03d.png -r 30 ThirdParty/AlphaPosePytorch/results/extractedFramesVideo.mp4
rm -r Temp
