ffmpeg -i inputVideo.mp4 -map 0:a audio.wav -map 0:v onlyvideo.avi
rm onlyvideo.avi
mv audio.wav ../Assets/Resources/audio.wav
cp background.png ../Assets/Resources/background.png
