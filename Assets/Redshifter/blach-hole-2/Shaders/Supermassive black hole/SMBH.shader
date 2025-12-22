Shader "KelvinvanHoorn/SMBH"
{
    Properties
    {
        _DiscTex ("Disc texture", 2D) = "white" {}
        _DiscWidth ("Width of the accretion disc", float) = 0.1
        _DiscOuterRadius ("Object relative disc outer radius", Range(0,1)) = 1
        _DiscInnerRadius ("Object relative disc inner radius", Range(0,1)) = 0.25
        _DiscSpeed ("Disc rotation speed", float) = 2
        [HDR]_DiscColor ("Disc main color", Color) = (1,0,0,1)
        _DopplerBeamingFactor ("Doppler beaming effect factor", float) = 66
        _HueRadius ("Hue shift start radius", Range(0,1)) = 0.75
        _HueShiftFactor ("Hue shifting factor", float) = -0.03
        _Steps ("Amount of steps", int) = 256
        _StepSize ("Step size", Range(0.001, 1)) = 0.1
        _SSRadius ("Object relative Schwarzschild radius", Range(0,1)) = 0.1
        _GConst ("Gravitational constant", float) = 0.3
        _BHBackground ("Background visible inside", Range(0,1)) = 0
        _HorizonSoft ("Horizon softness (world units)", Range(0,0.5)) = 0.5
        _EffectRadius ("Visible effect radius (object-relative)", Range(0,1)) = 0.6
        _EffectFeather ("Effect edge feather (object-relative)", Range(0,0.5)) = 0.05
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Cull Front
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        // Built-in replacement for URP's Opaque Texture:
        GrabPass { "_GrabTexture" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            static const float maxFloat = 3.402823466e+38;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 posCS : SV_POSITION;
                float3 posWS : TEXCOORD0;

                float3 centre : TEXCOORD1;
                float3 objectScale : TEXCOORD2;
            };

            sampler2D _GrabTexture;
            float4 _GrabTexture_TexelSize;

            sampler2D _DiscTex;
            float4 _DiscTex_ST;

            float _DiscWidth;
            float _DiscOuterRadius;
            float _DiscInnerRadius;
            float _DiscSpeed;

            float4 _DiscColor;
            float _DopplerBeamingFactor;
            float _HueRadius;
            float _HueShiftFactor;

            int _Steps;
            float _StepSize;

            float _SSRadius;
            float _GConst;

            float _BHBackground;
            float _HorizonSoft;
            float _EffectRadius;
            float _EffectFeather;
            
            v2f vert(appdata IN)
            {
                v2f OUT;
                OUT.posCS = UnityObjectToClipPos(IN.vertex);

                float4 wpos = mul(unity_ObjectToWorld, IN.vertex);
                OUT.posWS = wpos.xyz;

                OUT.centre = unity_ObjectToWorld._m03_m13_m23;

                OUT.objectScale = float3(
                    length(float3(unity_ObjectToWorld._m00, unity_ObjectToWorld._m10, unity_ObjectToWorld._m20)),
                    length(float3(unity_ObjectToWorld._m01, unity_ObjectToWorld._m11, unity_ObjectToWorld._m21)),
                    length(float3(unity_ObjectToWorld._m02, unity_ObjectToWorld._m12, unity_ObjectToWorld._m22))
                );

                return OUT;
            }

            float2 intersectSphere(float3 rayOrigin, float3 rayDir, float3 centre, float radius)
            {
                float3 offset = rayOrigin - centre;
                const float a = 1;
                float b = 2 * dot(offset, rayDir);
                float c = dot(offset, offset) - radius * radius;

                float discriminant = b * b - 4 * a * c;
                if (discriminant > 0)
                {
                    float s = sqrt(discriminant);
                    float dstToSphereNear = max(0, (-b - s) / (2 * a));
                    float dstToSphereFar  = (-b + s) / (2 * a);
                    if (dstToSphereFar >= 0)
                        return float2(dstToSphereNear, dstToSphereFar - dstToSphereNear);
                }
                return float2(maxFloat, 0);
            }

            float2 intersectInfiniteCylinder(float3 rayOrigin, float3 rayDir, float3 cylinderOrigin, float3 cylinderDir, float cylinderRadius)
            {
                float3 a0 = rayDir - dot(rayDir, cylinderDir) * cylinderDir;
                float a = dot(a0, a0);

                float3 dP = rayOrigin - cylinderOrigin;
                float3 c0 = dP - dot(dP, cylinderDir) * cylinderDir;
                float c = dot(c0, c0) - cylinderRadius * cylinderRadius;

                float b = 2 * dot(a0, c0);
                float discriminant = b * b - 4 * a * c;

                if (discriminant > 0)
                {
                    float s = sqrt(discriminant);
                    float dstToNear = max(0, (-b - s) / (2 * a));
                    float dstToFar  = (-b + s) / (2 * a);
                    if (dstToFar >= 0)
                        return float2(dstToNear, dstToFar - dstToNear);
                }
                return float2(maxFloat, 0);
            }

            float intersectInfinitePlane(float3 rayOrigin, float3 rayDir, float3 planeOrigin, float3 planeDir)
            {
                float b = dot(rayDir, planeDir);
                float c = dot(rayOrigin, planeDir) - dot(planeDir, planeOrigin);
                return -c / b;
            }

            float intersectDisc(float3 rayOrigin, float3 rayDir, float3 p1, float3 p2, float3 discDir, float discRadius, float innerRadius)
            {
                float discDst = maxFloat;
                float2 cylinderIntersection = intersectInfiniteCylinder(rayOrigin, rayDir, p1, discDir, discRadius);
                float cylinderDst = cylinderIntersection.x;

                if (cylinderDst < maxFloat)
                {
                    float finiteC1 = dot(discDir, rayOrigin + rayDir * cylinderDst - p1);
                    float finiteC2 = dot(discDir, rayOrigin + rayDir * cylinderDst - p2);

                    if (finiteC1 > 0 && finiteC2 < 0 && cylinderDst > 0)
                    {
                        discDst = cylinderDst;
                    }
                    else
                    {
                        float radiusSqr = discRadius * discRadius;
                        float innerRadiusSqr = innerRadius * innerRadius;

                        float p1Dst = max(intersectInfinitePlane(rayOrigin, rayDir, p1, discDir), 0);
                        float3 q1 = rayOrigin + rayDir * p1Dst;
                        float p1q1DstSqr = dot(q1 - p1, q1 - p1);

                        if (p1Dst > 0 && p1q1DstSqr < radiusSqr && p1q1DstSqr > innerRadiusSqr)
                            discDst = min(discDst, p1Dst);

                        float p2Dst = max(intersectInfinitePlane(rayOrigin, rayDir, p2, discDir), 0);
                        float3 q2 = rayOrigin + rayDir * p2Dst;
                        float p2q2DstSqr = dot(q2 - p2, q2 - p2);

                        if (p2Dst > 0 && p2q2DstSqr < radiusSqr && p2q2DstSqr > innerRadiusSqr)
                            discDst = min(discDst, p2Dst);
                    }
                }

                return discDst;
            }

            float remap(float v, float minOld, float maxOld, float minNew, float maxNew)
            {
                return minNew + (v - minOld) * (maxNew - minNew) / (maxOld - minOld);
            }

            float2 discUV(float3 planarDiscPos, float3 discDir, float3 centre, float radius)
            {
                float3 planarDiscPosNorm = normalize(planarDiscPos);
                float sampleDist01 = length(planarDiscPos) / radius;

                float3 tangentTestVector = float3(1,0,0);
                if (abs(dot(discDir, tangentTestVector)) >= 1) tangentTestVector = float3(0,1,0);

                float3 tangent = normalize(cross(discDir, tangentTestVector));
                float3 biTangent = cross(tangent, discDir);
                float phi = atan2(dot(planarDiscPosNorm, tangent), dot(planarDiscPosNorm, biTangent)) / UNITY_PI;
                phi = remap(phi, -1, 1, 0, 1);

                return float2(sampleDist01, phi);
            }

            float3 LinearToGammaSpace(float3 linRGB)
            {
                linRGB = max(linRGB, float3(0,0,0));
                return max(1.055 * pow(linRGB, 0.416666667) - 0.055, 0.0);
            }

            float3 GammaToLinearSpace(float3 sRGB)
            {
                return sRGB * (sRGB * (sRGB * 0.305306011 + 0.682171111) + 0.012522878);
            }

            float3 hdrIntensity(float3 emissiveColor, float intensity)
            {
                #ifndef UNITY_COLORSPACE_GAMMA
                    emissiveColor.rgb = LinearToGammaSpace(emissiveColor.rgb);
                #endif

                emissiveColor.rgb *= pow(2.0, intensity);

                #ifndef UNITY_COLORSPACE_GAMMA
                    emissiveColor.rgb = GammaToLinearSpace(emissiveColor.rgb);
                #endif

                return emissiveColor;
            }

            float3 RGBToHSV(float3 c)
            {
                float4 K = float4(0.0, -1.0/3.0, 2.0/3.0, -1.0);
                float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
                float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
                float d = q.x - min(q.w, q.y);
                float e = 1.0e-10;
                return float3(abs(q.z + (q.w - q.y) / (6.0*d + e)), d / (q.x + e), q.x);
            }

            float3 HSVToRGB(float3 c)
            {
                float4 K = float4(1.0, 2.0/3.0, 1.0/3.0, 3.0);
                float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
                return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
            }

            float3 RotateAboutAxis(float3 In, float3 Axis, float Rotation)
            {
                float s = sin(Rotation);
                float c = cos(Rotation);
                float one_minus_c = 1.0 - c;

                Axis = normalize(Axis);
                float3x3 rot_mat =
                {
                    one_minus_c*Axis.x*Axis.x + c,              one_minus_c*Axis.x*Axis.y - Axis.z*s,  one_minus_c*Axis.z*Axis.x + Axis.y*s,
                    one_minus_c*Axis.x*Axis.y + Axis.z*s,       one_minus_c*Axis.y*Axis.y + c,         one_minus_c*Axis.y*Axis.z - Axis.x*s,
                    one_minus_c*Axis.z*Axis.x - Axis.y*s,       one_minus_c*Axis.y*Axis.z + Axis.x*s,  one_minus_c*Axis.z*Axis.z + c
                };
                return mul(rot_mat, In);
            }

            float3 discColor(float3 baseColor, float3 planarDiscPos, float3 discDir, float3 cameraPos, float u, float radius)
            {
                float intensity = remap(u, 0, 1, 0.5, -1.2);
                intensity *= abs(intensity);

                float3 rotatePos = RotateAboutAxis(planarDiscPos, discDir, 0.01);
                float dopplerDistance = (length(rotatePos - cameraPos) - length(planarDiscPos - cameraPos)) / radius;
                intensity += dopplerDistance * _DiscSpeed * _DopplerBeamingFactor;

                float3 newColor = hdrIntensity(baseColor, intensity);

                float3 hueColor = RGBToHSV(newColor);
                float hueShift = saturate(remap(u, _HueRadius, 1, 0, 1));
                hueColor.r += hueShift * _HueShiftFactor;
                return HSVToRGB(hueColor);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float3 rayOrigin = _WorldSpaceCameraPos;
                float3 rayDir = normalize(IN.posWS - _WorldSpaceCameraPos);

                float sphereRadius = 0.5 * min(min(IN.objectScale.x, IN.objectScale.y), IN.objectScale.z);
                float2 outerSphereIntersection = intersectSphere(rayOrigin, rayDir, IN.centre, sphereRadius);

                float3 discDir = normalize(mul((float3x3)unity_ObjectToWorld, float3(0,1,0)));
                float3 p1 = IN.centre - 0.5 * _DiscWidth * discDir;
                float3 p2 = IN.centre + 0.5 * _DiscWidth * discDir;
                float discRadius = sphereRadius * _DiscOuterRadius;
                float innerRadius = sphereRadius * _DiscInnerRadius;

                float transmittance = 0;
                float blackHoleMask = 0;
                float3 samplePos = float3(maxFloat, 0, 0);
                float3 currentRayPos = rayOrigin + rayDir * outerSphereIntersection.x;
                float3 currentRayDir = rayDir;

                float ssR = _SSRadius * sphereRadius;

                float minDstToCentre = maxFloat;

                if (outerSphereIntersection.x < maxFloat)
                {
                    [loop]
                    for (int i = 0; i < _Steps; i++)
                    {
                        float3 dirToCentre = IN.centre - currentRayPos;
                        float dstToCentre = length(dirToCentre);
                        minDstToCentre = min(minDstToCentre, dstToCentre);
                        dirToCentre /= dstToCentre;

                        if (dstToCentre > sphereRadius + _StepSize) break;

                        float force = _GConst / (dstToCentre * dstToCentre);
                        currentRayDir = normalize(currentRayDir + dirToCentre * force * _StepSize);

                        currentRayPos += currentRayDir * _StepSize;

                        float blackHoleDistance = intersectSphere(currentRayPos, currentRayDir, IN.centre, _SSRadius * sphereRadius).x;
                        if (blackHoleDistance <= _StepSize)
                        {
                            float edge = max(_StepSize, _HorizonSoft);
                            // dstToCentre is already computed in your loop
                            blackHoleMask = smoothstep(ssR + edge, ssR, dstToCentre);
                            break;
                        }

                        float discDst = intersectDisc(currentRayPos, currentRayDir, p1, p2, discDir, discRadius, innerRadius);
                        if (transmittance < 1 && discDst < _StepSize)
                        {
                            transmittance = 1;
                            samplePos = currentRayPos + currentRayDir * discDst;
                        }
                    }
                }

                float effectR = sphereRadius * _EffectRadius;
                float feather = max(1e-4, sphereRadius * _EffectFeather);

                // 1 near center, 0 near outer sphere
                float effectMask = 1.0 - smoothstep(effectR, effectR + feather, minDstToCentre);

                // Always show disc + black core even if effectMask would be 0
                effectMask = max(effectMask, transmittance);
                effectMask = max(effectMask, blackHoleMask);
                float alpha = saturate(effectMask);

                float2 uv = float2(0,0);
                float3 planarDiscPos = 0;
                if (samplePos.x < maxFloat)
                {
                    planarDiscPos = samplePos - dot(samplePos - IN.centre, discDir) * discDir - IN.centre;
                    uv = discUV(planarDiscPos, discDir, IN.centre, discRadius);
                    uv.y += _Time.x * _DiscSpeed;
                }

                float texCol = tex2D(_DiscTex, uv * _DiscTex_ST.xy + _DiscTex_ST.zw).r;

                float2 screenUV = (IN.posCS.xy / IN.posCS.w);
                screenUV = screenUV * 0.5 + 0.5;

                float3 distortedRayDir = normalize(currentRayPos - rayOrigin);
                float3 rayCameraDir = mul((float3x3)UNITY_MATRIX_V, distortedRayDir);

                float4 rayUVProjection = mul(UNITY_MATRIX_P, float4(rayCameraDir, 0));
                float2 distortedScreenUV = rayUVProjection.xy * 0.5 + 0.5;

                float edgeFadex = smoothstep(0, 0.25, 1 - abs(remap(screenUV.x, 0, 1, -1, 1)));
                float edgeFadey = smoothstep(0, 0.25, 1 - abs(remap(screenUV.y, 0, 1, -1, 1)));
                float t = saturate(remap(outerSphereIntersection.y, sphereRadius, 2 * sphereRadius, 0, 1)) * edgeFadex * edgeFadey;
                distortedScreenUV = lerp(screenUV, distortedScreenUV, t);

                #if UNITY_UV_STARTS_AT_TOP
                if (_GrabTexture_TexelSize.y < 0) distortedScreenUV.y = 1 - distortedScreenUV.y;
                #endif

                float3 bg = tex2D(_GrabTexture, distortedScreenUV).rgb;

                // 0 = old behavior (black), 1 = fully visible even “inside”
                float bgMul = lerp(1.0, _BHBackground, blackHoleMask);

                float3 backgroundCol = bg * bgMul;


                float3 discCol = discColor(_DiscColor.rgb, planarDiscPos, discDir, _WorldSpaceCameraPos, uv.x, discRadius);

                transmittance *= texCol * _DiscColor.a;
                float3 col = lerp(backgroundCol, discCol, transmittance);
                col = lerp(col, 0.0, blackHoleMask);
                return float4(col, alpha);
            }
            ENDCG
        }
    }
}
