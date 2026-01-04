using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Clump
{
    [Serializable]
    public struct ClumpParameters
    {
        public float pullToCentre;
        public float pointToSameDirection;
        public float baseHeight;
        public float heightRandom;
        public float baseWidth;
        public float widthRandom;
        public float baseTilt;
        public float tiltRandom;
        public float baseBend;
        public float bendRandom;
    }
}