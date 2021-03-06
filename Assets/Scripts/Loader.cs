﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using Dummiesman;

namespace SUNCGLoader { 

    public class Loader {

        private House house;

        public GameObject HouseToScene(House house)
        {
            this.house = house;
            GameObject root = new GameObject("House_" + house.id);
            root.tag = "RT";
            foreach (Level level in house.levels)
            {
                LoadLevel(root, level);
            }
            return root;
        }

        private void LoadLevel(GameObject root, Level level)
        {
            GameObject levelRoot = new GameObject("Level_" + level.id);
            levelRoot.transform.parent = root.transform;
            foreach (Node n in level.nodes)
            {
                LoadNode(levelRoot, n);
            }

        }

        private void LoadNode(GameObject levelRoot, Node node)
        { 
            if (node.valid == 1)
            {
                GameObject nodeObj;
                switch (node.type)
                {
                    case "Object":
                        LoadNodeMesh(node, out nodeObj);
                        nodeObj.name = "Node_" + node.modelId;
                        nodeObj.transform.parent = levelRoot.transform;

                        break;
                    case "Room":
                        string[] wallsFloorCeiling = new string[] { "w", "f", "c" };
                        foreach (string extension in wallsFloorCeiling)
                        {
                            bool loaded = LoadNodeMesh(node, out nodeObj, extension);
                            if (loaded)
                            {
                                nodeObj.name = "Node_" + node.modelId + extension;
                                nodeObj.transform.parent = levelRoot.transform;
                            }
                            //Not all 3 exists for all
                            else
                            {
                                GameObject.Destroy(nodeObj);
                            }
                        }
                        break;
                    case "Ground":
                        LoadNodeMesh(node, out nodeObj, "f");
                        nodeObj.name = "Node_" + node.modelId + "f";
                        nodeObj.transform.parent = levelRoot.transform;

                        break;
                    case "Box":
                        //TODO: This is probably not great if we can't load these
                        break;
                    default:
                        Debug.LogError($"Unhandled node type: {node.type}");
                        // TODO: If this ever fails due to box type being unknown we need to catch this
                        break;
                }
            }
        }

        // New and improved load OBJ to GameObject
        private bool LoadNodeMesh(Node node, out GameObject nodeObj, string modelIdAppend = "")
        {

            string pathToObj = Config.SUNCGDataPath;
            if (node.type == "Room" || node.type == "Ground")
            {
                pathToObj += "room/" + this.house.id + "/" + node.modelId + modelIdAppend + ".obj";
            }
            else if (node.type == "Object")
            {
                pathToObj += "object/" + node.modelId + "/" + node.modelId + modelIdAppend + ".obj";
            }
            if (!File.Exists(pathToObj))
            {
                nodeObj = null;
                return false;
            }

            nodeObj = new OBJLoader().Load(pathToObj);

            // Room meshes have no transform.
            if (node.transform != null)
            {
                Vector4 column1 = new Vector4(node.transform[0], node.transform[1], node.transform[2], node.transform[3]);
                Vector4 column2 = new Vector4(node.transform[4], node.transform[5], node.transform[6], node.transform[7]);
                Vector4 column3 = new Vector4(node.transform[8], node.transform[9], node.transform[10], node.transform[11]);
                Vector4 column4 = new Vector4(node.transform[12], node.transform[13], node.transform[14], node.transform[15]);
                Matrix4x4 objToWorld = new Matrix4x4(column1, column2, column3, column4);
                SetTransformFromMatrix(nodeObj.transform, ref objToWorld);
            } else {
                Matrix4x4 objToWorld = Matrix4x4.identity;
                SetTransformFromMatrix(nodeObj.transform, ref objToWorld);
            }

            // OBJ vs. Unity must convert between right-handed and left-handed
            // coordinate systems via a flip of the X axis, see:
            // https://gamedev.stackexchange.com/questions/39906/why-does-unity-obj-import-flip-my-x-coordinate
            //nodeObj.transform.localScale = new Vector3(
                //nodeObj.transform.localScale.x * -1.0f,
                //nodeObj.transform.localScale.y,
                //nodeObj.transform.localScale.z);

            return true;
        }

        private UnityEngine.Material LoadMaterial(Material suncgMat)
        {
            UnityEngine.Material mat = new UnityEngine.Material(Shader.Find(Config.defaultShader));
            mat.name = suncgMat.name;
            Color c = Color.white;
            ColorUtility.TryParseHtmlString(suncgMat.diffuse, out c);
            mat.color = c;
            if(suncgMat.texture != null)
            {
                LoadSunCGTextureIntoMaterial(suncgMat.texture, mat);
            }
            return mat;
        }

        private static Texture2D LoadJPG(string filePath)
        {

            Texture2D tex = null;
            byte[] fileData;

            if (File.Exists(filePath))
            {
                fileData = File.ReadAllBytes(filePath);
                tex = new Texture2D(1, 1, TextureFormat.DXT1, false);
                bool success = tex.LoadImage(fileData);
                if (!success) {
                    Debug.LogWarning("Failed to load image at " + filePath);
                }
            }
            return tex;
        }

        private void LoadSunCGTextureIntoMaterial(string textureName, UnityEngine.Material mat)
        {
            string texturePath = Config.SUNCGDataPath + "texture/" + textureName + ".jpg";
            Texture2D readTex = LoadJPG(texturePath);

            // See: https://answers.unity.com/questions/10292/how-do-i-generate-mipmaps-at-runtime.html
            // We need to force generation of mip maps
            Texture2D tex = new Texture2D(readTex.width, readTex.height, TextureFormat.RGB24, true);
            tex.SetPixels(readTex.GetPixels());
            tex.Apply();
            tex.filterMode = FilterMode.Trilinear;
            mat.mainTexture = tex;
        }

        #region matrixUtilities
        //matrix utilities
        public static Vector3 ExtractTranslationFromMatrix(ref Matrix4x4 matrix)
        {
            Vector3 translate;
            translate.x = matrix.m03;
            translate.y = matrix.m13;
            translate.z = matrix.m23;
            return translate;
        }

        public static Quaternion ExtractRotationFromMatrix(ref Matrix4x4 matrix)
        {
            Vector3 forward;
            forward.x = matrix.m02;
            forward.y = matrix.m12;
            forward.z = matrix.m22;

            Vector3 upwards;
            upwards.x = matrix.m01;
            upwards.y = matrix.m11;
            upwards.z = matrix.m21;

            return Quaternion.LookRotation(forward, upwards);
        }


        public static Vector3 ExtractScaleFromMatrix(ref Matrix4x4 matrix)
        {
            Vector3 scale;
            scale.x = new Vector4(matrix.m00, matrix.m10, matrix.m20, matrix.m30).magnitude;
            scale.y = new Vector4(matrix.m01, matrix.m11, matrix.m21, matrix.m31).magnitude;
            scale.z = new Vector4(matrix.m02, matrix.m12, matrix.m22, matrix.m32).magnitude;
            return scale;
        }

        
        public static void DecomposeMatrix(ref Matrix4x4 matrix, out Vector3 localPosition, out Quaternion localRotation, out Vector3 localScale)
        {
            localPosition = ExtractTranslationFromMatrix(ref matrix);
            localRotation = ExtractRotationFromMatrix(ref matrix);
            localScale = ExtractScaleFromMatrix(ref matrix);
        }

        
        public static void SetTransformFromMatrix(Transform transform, ref Matrix4x4 matrix)
        {
            transform.localPosition = ExtractTranslationFromMatrix(ref matrix);
            transform.localRotation = ExtractRotationFromMatrix(ref matrix);
            transform.localScale = ExtractScaleFromMatrix(ref matrix);
        }

        public static readonly Quaternion IdentityQuaternion = Quaternion.identity;
       
        public static readonly Matrix4x4 IdentityMatrix = Matrix4x4.identity;

        public static Matrix4x4 TranslationMatrix(Vector3 offset)
        {
            Matrix4x4 matrix = IdentityMatrix;
            matrix.m03 = offset.x;
            matrix.m13 = offset.y;
            matrix.m23 = offset.z;
            return matrix;
        }
        #endregion
    }

}
