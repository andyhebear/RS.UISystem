using UnityEngine;
using UnityEngine.UI;
using RS.Unity3DLib.UISystem.Utils;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UI;
#endif
namespace RS.Unity3DLib.UISystem.UIEffect
{
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    //[AddComponentMenu("UISystem/Masks/Image Rounded Mask")]
    public class ImageRoundedMask : MonoBehaviour
    {
        private static readonly int Props = Shader.PropertyToID("_WidthHeightRadius");
        
        public float radius = 40f;          
        private Material material;
        
        [HideInInspector, SerializeField] private MaskableGraphic image;
        
        private void OnValidate() {
            Validate();
            Refresh();
        }
        
        private void OnDestroy() {
            image.material = null;
            DestroyHelper.Destroy(material);
            image = null;
            material = null;
        }

        private void OnEnable() {
            var other = GetComponent<ImageIndependentRoundedMask>();
            if (other != null) {
                radius = other.r.x; //When it does, transfer the radius value to this script
                DestroyHelper.Destroy(other);
            }
            
            Validate();
            Refresh();
        }
        
        private void OnRectTransformDimensionsChange() {
            if (enabled && material != null) {
                Refresh();
            }
        }

        public void Validate() {
            var isDirty = false;
            if (material == null) {
                material = new Material(Shader.Find("RS/UISystem/UIRoundedCorners"));
                isDirty = true;
            }

            if (image == null) {
                TryGetComponent(out image);
                isDirty = true;
            }

            if (image != null) {
                image.material = material;
                isDirty = true;
            }
            
#if UNITY_EDITOR
            if (isDirty)
                UnityEditor.EditorUtility.SetDirty(gameObject);
#endif
        }
        
        public void Refresh() {
            var rect = ((RectTransform)transform).rect;
            material.SetVector(Props, new Vector4(rect.width, rect.height, radius * 2, 0));   
        }
    }


#if UNITY_EDITOR
    [CustomEditor(typeof(ImageRoundedMask))]
    public class ImageRoundedMaskInspector : UnityEditor.Editor
    {
        private ImageRoundedMask script;

        private void OnEnable() {
            script = (ImageRoundedMask)target;
        }

        public override void OnInspectorGUI() {
            base.OnInspectorGUI();

            if (!script.TryGetComponent<MaskableGraphic>(out var _)) {
                EditorGUILayout.HelpBox("This script requires an MaskableGraphic (Image or RawImage) component on the same gameobject",MessageType.Warning);
            }
        }
    }
#endif
}