using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RS.Unity3DLib.UISystem.UIEffect
{
    public class SpriteSequenceFrame : MonoBehaviour
    {
        public Sprite[] sprites;

        private Image image;
        private SpriteRenderer spriteRenderer;

        private int currentFrame;
        private int totalFrame;
        private bool isPlay = false;

        //public string atlasName = "";
        //public int aniNum = 16;
        public float loopInterval = 0;
        [Header("每秒更新次数")]
        public int frameRate = 2;
        private float updateDeltaTime;

        private float lastUpdateTime;
        public bool loop = false;

        public bool autoPlay = false;

        //private List<string> spriteNames = new List<string>();

        private void Awake() {
            //for (int i = 0; i < aniNum; i++) {
            //    spriteNames.Add(atlasName + "_" + i);
            //}
            if (image == null) {
                image = GetComponent<Image>();
            }

            if (spriteRenderer == null) {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }
            totalFrame = (sprites == null ? 0 : sprites.Length)+1;
        }

        private void OnEnable() {
            if (autoPlay) {
                Play();
            }
        }

        [UnityEngine.ContextMenu("Play")]
        public void Play() {


            if (image || spriteRenderer) {
                currentFrame = 0;
                //totalFrame = aniNum;
                updateDeltaTime = 1 / (float)frameRate;
                lastUpdateTime = Time.time;
                isPlay = true;
                SetTexture();
            }
        }

        [UnityEngine.ContextMenu("Stop")]
        public void Stop() {
            isPlay = false;
        }

        // Update is called once per frame
        void Update() {
            if (isPlay) {
                var deltaTime = Time.time - lastUpdateTime;
                if (deltaTime > updateDeltaTime) {
                    currentFrame = currentFrame + 1;
                    if (currentFrame >= totalFrame) {
                        if (loop) {
                            currentFrame = currentFrame - totalFrame;
                        }
                        else {
                            Stop();
                            return;
                        }
                    }

                    SetTexture();
                    if (loop && currentFrame == totalFrame - 1) {
                        lastUpdateTime += updateDeltaTime + loopInterval;
                    }
                    else {
                        lastUpdateTime += updateDeltaTime;
                    }
                }
            }
        }

        private void SetTexture() {
            if (image) {
                if (sprites.Length > 0)
                    image.sprite = sprites[currentFrame % totalFrame];
            }
            else if (spriteRenderer) {
                if (sprites.Length > 0)
                    spriteRenderer.sprite = sprites[currentFrame % totalFrame];
            }
        }
    }
}