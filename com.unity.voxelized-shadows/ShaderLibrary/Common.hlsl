#ifndef UNITY_VX_SHADOWMAPS_COMMON_INCLUDED
#define UNITY_VX_SHADOWMAPS_COMMON_INCLUDED

#define USE_EMULATE_COUNTBITS
#define OFFSET_DIR 15
#define OFFSET_POINT 15 // TODO : define it for point light
#define OFFSET_SPOT 15 // TODO : define it for spot light

#define VX_SHADOWS_ONLY  0x80000000
#define VX_SHADOWS_BLEND 0x40000000

StructuredBuffer<uint> _VxShadowMapsBuffer;


uint emulateCLZ(uint x)
{
    // emulate it similar to count leading zero.
    // count leading 1bit.

    uint n = 32;
    uint y;

    y = x >> 16; if (y != 0) { n = n - 16; x = y; }
    y = x >>  8; if (y != 0) { n = n -  8; x = y; }
    y = x >>  4; if (y != 0) { n = n -  4; x = y; }
    y = x >>  2; if (y != 0) { n = n -  2; x = y; }
    y = x >>  1; if (y != 0) return n - 2;

    return n - x;
}

uint countBits(uint i)
{
#ifdef USE_EMULATE_COUNTBITS
    i = i - ((i >> 1) & 0x55555555);
    i = (i & 0x33333333) + ((i >> 2) & 0x33333333);

    return (((i + (i >> 4)) & 0x0F0F0F0F) * 0x01010101) >> 24;
#else
    return countbits(i);
#endif
}

uint4 countBits(uint4 i)
{
#ifdef USE_EMULATE_COUNTBITS
    i = i - ((i >> 1) & 0x55555555);
    i = (i & 0x33333333) + ((i >> 2) & 0x33333333);

    return (((i + (i >> 4)) & 0x0F0F0F0F) * 0x01010101) >> 24;
#else
    return countbits(i);
#endif
}

// todo : calculate uint2 and more?
uint CalculateRescale(uint srcPosbit, uint dstPosbit)
{
    return 32 - emulateCLZ(srcPosbit ^ dstPosbit);
}

void TraverseVxShadowMapPosQ(uint begin, uint typeOffset, uint3 posQ, out uint4 result)
{
    uint vxsmOffset = begin + typeOffset;
    uint dagScale = _VxShadowMapsBuffer[begin + 2];

    uint nodeIndex = 0;
    uint scale = dagScale;

    bool lit = false;
    bool shadowed = false;
    bool intersected = true;

    for (; scale > 3 && intersected; --scale)
    {
        // calculate where to go to child
        uint3 childDet = (posQ >> (scale - 1)) & 0x00000001;
        uint cellShift = (childDet.x << 1) + (childDet.y << 2) + (childDet.z << 3);
        uint cellbit   = 0x00000003 << cellShift;

        // calculate bit
        uint header = _VxShadowMapsBuffer[vxsmOffset + nodeIndex];
        uint childmask = header >> 16;
        uint shadowbit = (childmask & cellbit) >> cellShift;

        // determine whether it is lit or shadowed.
        lit      = shadowbit & 0x00000001;
        shadowed = shadowbit & 0x00000002;

        // if it has lit and shadowed, it is not decided yet(need to traverse more)
        intersected = lit && shadowed;

        // find next child node
        uint mask = ~(0xFFFFFFFF << cellShift);
        uint childrenbit = childmask & ((childmask & 0x0000AAAA) >> 1);
        uint childIndex = countBits(childrenbit & mask);

        // go down to the next node
        nodeIndex = _VxShadowMapsBuffer[vxsmOffset + nodeIndex + 1 + childIndex];
    }

    result = uint4(nodeIndex, lit, shadowed, intersected);
}

void TraverseVxShadowMapPosQ2x2(uint begin, uint typeOffset, uint3 posQ_0, out uint4 results[4])
{
    uint vxsmOffset = begin + typeOffset;
    uint dagScale = _VxShadowMapsBuffer[begin + 2];

    uint3 posQ_1 = posQ_0 + uint3(1, 0, 0);
    uint3 posQ_2 = posQ_0 + uint3(0, 1, 0);
    uint3 posQ_3 = posQ_0 + uint3(1, 1, 0);

    uint4 nodeIndex4 = 0;
    uint scale = dagScale;

    bool4 lit4 = false;
    bool4 shadowed4 = false;
    bool4 intersected4 = true;

    for (; scale > 3 && any(intersected4); --scale)
    {
        // calculate where to go to child
        uint3 childDet_0 = (posQ_0 >> (scale - 1)) & 0x00000001;
        uint3 childDet_1 = (posQ_1 >> (scale - 1)) & 0x00000001;
        uint3 childDet_2 = (posQ_2 >> (scale - 1)) & 0x00000001;
        uint3 childDet_3 = (posQ_3 >> (scale - 1)) & 0x00000001;

        uint4 cellShift4 = uint4(
            (childDet_0.x << 1) + (childDet_0.y << 2) + (childDet_0.z << 3),
            (childDet_1.x << 1) + (childDet_1.y << 2) + (childDet_1.z << 3),
            (childDet_2.x << 1) + (childDet_2.y << 2) + (childDet_2.z << 3),
            (childDet_3.x << 1) + (childDet_3.y << 2) + (childDet_3.z << 3));

        uint4 cellbit4 = 0x00000003 << cellShift4;

        // calculate bit
        uint4 header4 = uint4(
            _VxShadowMapsBuffer[vxsmOffset + nodeIndex4.x],
            _VxShadowMapsBuffer[vxsmOffset + nodeIndex4.y],
            _VxShadowMapsBuffer[vxsmOffset + nodeIndex4.z],
            _VxShadowMapsBuffer[vxsmOffset + nodeIndex4.w]);
        uint4 childmask4 = header4 >> 16;
        uint4 shadowbit4 = (childmask4 & cellbit4) >> cellShift4;

        // determine whether it is lit or shadowed.
        lit4      = intersected4 ? shadowbit4 & 0x00000001 : lit4;
        shadowed4 = intersected4 ? shadowbit4 & 0x00000002 : shadowed4;

        // if it has lit and shadowed, it is not decided yet(need to traverse more)
        intersected4 = lit4 && shadowed4;

        // find next child node
        uint4 mask4 = ~(0xFFFFFFFF << cellShift4);
        uint4 childrenbit4 = childmask4 & ((childmask4 & 0x0000AAAA) >> 1);
        uint4 childIndex4 = countBits(childrenbit4 & mask4);
        uint4 nextIndex4 = vxsmOffset + nodeIndex4 + 1 + childIndex4;

        // go down to the next node
        nodeIndex4.x = intersected4.x ? _VxShadowMapsBuffer[nextIndex4.x] : nodeIndex4.x;
        nodeIndex4.y = intersected4.y ? _VxShadowMapsBuffer[nextIndex4.y] : nodeIndex4.y;
        nodeIndex4.z = intersected4.z ? _VxShadowMapsBuffer[nextIndex4.z] : nodeIndex4.z;
        nodeIndex4.w = intersected4.w ? _VxShadowMapsBuffer[nextIndex4.w] : nodeIndex4.w;
    }

    results[0] = uint4(nodeIndex4.x, lit4.x, shadowed4.x, intersected4.x);
    results[1] = uint4(nodeIndex4.y, lit4.y, shadowed4.y, intersected4.y);
    results[2] = uint4(nodeIndex4.z, lit4.z, shadowed4.z, intersected4.z);
    results[3] = uint4(nodeIndex4.w, lit4.w, shadowed4.w, intersected4.w);
}

void TraverseVxShadowMapPosQ2x2x2(uint begin, uint typeOffset, uint3 posQ_0, out uint4 results[8])
{
    uint vxsmOffset = begin + typeOffset;
    uint dagScale = _VxShadowMapsBuffer[begin + 2];

    uint3 posQ_1 = posQ_0 + uint3(1, 0, 0);
    uint3 posQ_2 = posQ_0 + uint3(0, 1, 0);
    uint3 posQ_3 = posQ_0 + uint3(1, 1, 0);
    uint3 posQ_4 = posQ_0 + uint3(0, 0, 1);
    uint3 posQ_5 = posQ_0 + uint3(1, 0, 1);
    uint3 posQ_6 = posQ_0 + uint3(0, 1, 1);
    uint3 posQ_7 = posQ_0 + uint3(1, 1, 1);

    uint4 nodeIndex4_0 = 0;
    uint4 nodeIndex4_1 = 0;
    uint scale = dagScale;

    bool4 lit4_0 = false;
    bool4 lit4_1 = false;
    bool4 shadowed4_0 = false;
    bool4 shadowed4_1 = false;
    bool4 intersected4_0 = true;
    bool4 intersected4_1 = true;

    for (; scale > 3 && any(intersected4_0 || intersected4_1); --scale)
    {
        // calculate where to go to child
        uint3 childDet_0 = (posQ_0 >> (scale - 1)) & 0x00000001;
        uint3 childDet_1 = (posQ_1 >> (scale - 1)) & 0x00000001;
        uint3 childDet_2 = (posQ_2 >> (scale - 1)) & 0x00000001;
        uint3 childDet_3 = (posQ_3 >> (scale - 1)) & 0x00000001;
        uint3 childDet_4 = (posQ_4 >> (scale - 1)) & 0x00000001;
        uint3 childDet_5 = (posQ_5 >> (scale - 1)) & 0x00000001;
        uint3 childDet_6 = (posQ_6 >> (scale - 1)) & 0x00000001;
        uint3 childDet_7 = (posQ_7 >> (scale - 1)) & 0x00000001;

        uint4 cellShift4_0 = uint4(
            (childDet_0.x << 1) + (childDet_0.y << 2) + (childDet_0.z << 3),
            (childDet_1.x << 1) + (childDet_1.y << 2) + (childDet_1.z << 3),
            (childDet_2.x << 1) + (childDet_2.y << 2) + (childDet_2.z << 3),
            (childDet_3.x << 1) + (childDet_3.y << 2) + (childDet_3.z << 3));
        uint4 cellShift4_1 = uint4(
            (childDet_4.x << 1) + (childDet_4.y << 2) + (childDet_4.z << 3),
            (childDet_5.x << 1) + (childDet_5.y << 2) + (childDet_5.z << 3),
            (childDet_6.x << 1) + (childDet_6.y << 2) + (childDet_6.z << 3),
            (childDet_7.x << 1) + (childDet_7.y << 2) + (childDet_7.z << 3));

        uint4 cellbit4_0 = 0x00000003 << cellShift4_0;
        uint4 cellbit4_1 = 0x00000003 << cellShift4_1;

        // calculate bit
        uint4 header4_0 = uint4(
            _VxShadowMapsBuffer[vxsmOffset + nodeIndex4_0.x],
            _VxShadowMapsBuffer[vxsmOffset + nodeIndex4_0.y],
            _VxShadowMapsBuffer[vxsmOffset + nodeIndex4_0.z],
            _VxShadowMapsBuffer[vxsmOffset + nodeIndex4_0.w]);
        uint4 header4_1 = uint4(
            _VxShadowMapsBuffer[vxsmOffset + nodeIndex4_1.x],
            _VxShadowMapsBuffer[vxsmOffset + nodeIndex4_1.y],
            _VxShadowMapsBuffer[vxsmOffset + nodeIndex4_1.z],
            _VxShadowMapsBuffer[vxsmOffset + nodeIndex4_1.w]);

        uint4 childmask4_0 = header4_0 >> 16;
        uint4 childmask4_1 = header4_1 >> 16;

        uint4 shadowbit4_0 = (childmask4_0 & cellbit4_0) >> cellShift4_0;
        uint4 shadowbit4_1 = (childmask4_1 & cellbit4_1) >> cellShift4_1;

        // determine whether it is lit or shadowed.
        lit4_0      = intersected4_0 ? shadowbit4_0 & 0x00000001 : lit4_0;
        lit4_1      = intersected4_1 ? shadowbit4_1 & 0x00000001 : lit4_1;
        shadowed4_0 = intersected4_0 ? shadowbit4_0 & 0x00000002 : shadowed4_0;
        shadowed4_1 = intersected4_1 ? shadowbit4_1 & 0x00000002 : shadowed4_1;

        // if it has lit and shadowed, it is not decided yet(need to traverse more)
        intersected4_0 = lit4_0 && shadowed4_0;
        intersected4_1 = lit4_1 && shadowed4_1;

        // find next child node
        uint4 mask4_0 = ~(0xFFFFFFFF << cellShift4_0);
        uint4 mask4_1 = ~(0xFFFFFFFF << cellShift4_1);
        uint4 childrenbit4_0 = childmask4_0 & ((childmask4_0 & 0x0000AAAA) >> 1);
        uint4 childrenbit4_1 = childmask4_1 & ((childmask4_1 & 0x0000AAAA) >> 1);
        uint4 childIndex4_0 = countBits(childrenbit4_0 & mask4_0);
        uint4 childIndex4_1 = countBits(childrenbit4_1 & mask4_1);
        uint4 nextIndex4_0 = vxsmOffset + nodeIndex4_0 + 1 + childIndex4_0;
        uint4 nextIndex4_1 = vxsmOffset + nodeIndex4_1 + 1 + childIndex4_1;

        // go down to the next node
        nodeIndex4_0.x = intersected4_0.x ? _VxShadowMapsBuffer[nextIndex4_0.x] : nodeIndex4_0.x;
        nodeIndex4_0.y = intersected4_0.y ? _VxShadowMapsBuffer[nextIndex4_0.y] : nodeIndex4_0.y;
        nodeIndex4_0.z = intersected4_0.z ? _VxShadowMapsBuffer[nextIndex4_0.z] : nodeIndex4_0.z;
        nodeIndex4_0.w = intersected4_0.w ? _VxShadowMapsBuffer[nextIndex4_0.w] : nodeIndex4_0.w;
        nodeIndex4_1.x = intersected4_1.x ? _VxShadowMapsBuffer[nextIndex4_1.x] : nodeIndex4_1.x;
        nodeIndex4_1.y = intersected4_1.y ? _VxShadowMapsBuffer[nextIndex4_1.y] : nodeIndex4_1.y;
        nodeIndex4_1.z = intersected4_1.z ? _VxShadowMapsBuffer[nextIndex4_1.z] : nodeIndex4_1.z;
        nodeIndex4_1.w = intersected4_1.w ? _VxShadowMapsBuffer[nextIndex4_1.w] : nodeIndex4_1.w;
    }

    results[0] = uint4(nodeIndex4_0.x, lit4_0.x, shadowed4_0.x, intersected4_0.x);
    results[1] = uint4(nodeIndex4_0.y, lit4_0.y, shadowed4_0.y, intersected4_0.y);
    results[2] = uint4(nodeIndex4_0.z, lit4_0.z, shadowed4_0.z, intersected4_0.z);
    results[3] = uint4(nodeIndex4_0.w, lit4_0.w, shadowed4_0.w, intersected4_0.w);
    results[4] = uint4(nodeIndex4_1.x, lit4_1.x, shadowed4_1.x, intersected4_1.x);
    results[5] = uint4(nodeIndex4_1.y, lit4_1.y, shadowed4_1.y, intersected4_1.y);
    results[6] = uint4(nodeIndex4_1.z, lit4_1.z, shadowed4_1.z, intersected4_1.z);
    results[7] = uint4(nodeIndex4_1.w, lit4_1.w, shadowed4_1.w, intersected4_1.w);
}

float TraverseNearestSampleVxShadowMap(uint begin, uint typeOffset, uint3 posQ, uint4 innerResult)
{
    uint vxsmOffset = begin + typeOffset;
    uint nodeIndex = innerResult.x;

    uint3 leaf = posQ % uint3(8, 8, 8);
    uint leafIndex = _VxShadowMapsBuffer[vxsmOffset + nodeIndex + leaf.z];

    uint bitmask0 = _VxShadowMapsBuffer[vxsmOffset + leafIndex];
    uint bitmask1 = _VxShadowMapsBuffer[vxsmOffset + leafIndex + 1];
    uint bitmask = leaf.y < 4 ? bitmask0 : bitmask1;

    uint maskShift = leaf.x + 8 * (leaf.y % 4);
    uint mask = 0x00000001 << maskShift;

    float attenuation = (bitmask & mask) == 0 ? 1.0 : 0.0;

    return attenuation;
}

float TraverseBilinearSampleVxShadowMap(uint begin, uint typeOffset, uint3 posQ_0, uint4 innerResults[4], float2 lerpWeight)
{
    uint vxsmOffset = begin + typeOffset;
    uint4 nodeIndex4 = vxsmOffset + uint4(
        innerResults[0].x,
        innerResults[1].x,
        innerResults[2].x,
        innerResults[3].x);

    uint3 posQ_1 = posQ_0 + uint3(1, 0, 0);
    uint3 posQ_2 = posQ_0 + uint3(0, 1, 0);
    uint3 posQ_3 = posQ_0 + uint3(1, 1, 0);

    uint4 leaf4_x = uint4(posQ_0.x % 8, posQ_1.x % 8, posQ_2.x % 8, posQ_3.x % 8);
    uint4 leaf4_y = uint4(posQ_0.y % 8, posQ_1.y % 8, posQ_2.y % 8, posQ_3.y % 8);
    uint4 leaf4_z = uint4(posQ_0.z % 8, posQ_1.z % 8, posQ_2.z % 8, posQ_3.z % 8);

    uint4 leafIndex = vxsmOffset + uint4(
        _VxShadowMapsBuffer[nodeIndex4.x + leaf4_z.x],
        _VxShadowMapsBuffer[nodeIndex4.y + leaf4_z.y],
        _VxShadowMapsBuffer[nodeIndex4.z + leaf4_z.z],
        _VxShadowMapsBuffer[nodeIndex4.w + leaf4_z.w]);

    uint4 bitmask04 = uint4(
        innerResults[0].y ? 0x00000000 : 0xFFFFFFFF,
        innerResults[1].y ? 0x00000000 : 0xFFFFFFFF,
        innerResults[2].y ? 0x00000000 : 0xFFFFFFFF,
        innerResults[3].y ? 0x00000000 : 0xFFFFFFFF);
    uint4 bitmask14 = bitmask04;

    if (innerResults[0].w)
    {
        bitmask04.x = _VxShadowMapsBuffer[leafIndex.x];
        bitmask14.x = _VxShadowMapsBuffer[leafIndex.x + 1];
    }
    if (innerResults[1].w)
    {
        bitmask04.y = _VxShadowMapsBuffer[leafIndex.y];
        bitmask14.y = _VxShadowMapsBuffer[leafIndex.y + 1];
    }
    if (innerResults[2].w)
    {
        bitmask04.z = _VxShadowMapsBuffer[leafIndex.z];
        bitmask14.z = _VxShadowMapsBuffer[leafIndex.z + 1];
    }
    if (innerResults[3].w)
    {
        bitmask04.w = _VxShadowMapsBuffer[leafIndex.w];
        bitmask14.w = _VxShadowMapsBuffer[leafIndex.w + 1];
    }

    uint4 bitmask4 = leaf4_y < 4 ? bitmask04 : bitmask14;

    uint4 maskShift4 = leaf4_x + 8 * (leaf4_y % 4);
    uint4 mask4 = uint4(1, 1, 1, 1) << maskShift4;

    float4 attenuation4 = (bitmask4 & mask4) == 0 ? 1.0 : 0.0;
    attenuation4.xy = lerp(attenuation4.xz, attenuation4.yw, lerpWeight.x);
    attenuation4.x  = lerp(attenuation4.x,  attenuation4.y,  lerpWeight.y);

    return attenuation4.x;
}

float TravereTrilinearSampleVxShadowMap(uint begin, uint typeOffset, uint3 posQ_0, uint4 innerResults[8], float3 lerpWeight)
{
    uint vxsmOffset = begin + typeOffset;
    uint4 nodeIndex4_0 = vxsmOffset + uint4(
        innerResults[0].x,
        innerResults[1].x,
        innerResults[2].x,
        innerResults[3].x);
    uint4 nodeIndex4_1 = vxsmOffset + uint4(
        innerResults[4].x,
        innerResults[5].x,
        innerResults[6].x,
        innerResults[7].x);

    uint3 posQ_1 = posQ_0 + uint3(1, 0, 0);
    uint3 posQ_2 = posQ_0 + uint3(0, 1, 0);
    uint3 posQ_3 = posQ_0 + uint3(1, 1, 0);
    uint3 posQ_4 = posQ_0 + uint3(0, 0, 1);
    uint3 posQ_5 = posQ_0 + uint3(1, 0, 1);
    uint3 posQ_6 = posQ_0 + uint3(0, 1, 1);
    uint3 posQ_7 = posQ_0 + uint3(1, 1, 1);

    uint4 leaf4_x0 = uint4(posQ_0.x % 8, posQ_1.x % 8, posQ_2.x % 8, posQ_3.x % 8);
    uint4 leaf4_y0 = uint4(posQ_0.y % 8, posQ_1.y % 8, posQ_2.y % 8, posQ_3.y % 8);
    uint4 leaf4_z0 = uint4(posQ_0.z % 8, posQ_1.z % 8, posQ_2.z % 8, posQ_3.z % 8);
    uint4 leaf4_x1 = uint4(posQ_4.x % 8, posQ_5.x % 8, posQ_6.x % 8, posQ_7.x % 8);
    uint4 leaf4_y1 = uint4(posQ_4.y % 8, posQ_5.y % 8, posQ_6.y % 8, posQ_7.y % 8);
    uint4 leaf4_z1 = uint4(posQ_4.z % 8, posQ_5.z % 8, posQ_6.z % 8, posQ_7.z % 8);

    uint4 leafIndex_0 = vxsmOffset + uint4(
        _VxShadowMapsBuffer[nodeIndex4_0.x + leaf4_z0.x],
        _VxShadowMapsBuffer[nodeIndex4_0.y + leaf4_z0.y],
        _VxShadowMapsBuffer[nodeIndex4_0.z + leaf4_z0.z],
        _VxShadowMapsBuffer[nodeIndex4_0.w + leaf4_z0.w]);
    uint4 leafIndex_1 = vxsmOffset + uint4(
        _VxShadowMapsBuffer[nodeIndex4_1.x + leaf4_z1.x],
        _VxShadowMapsBuffer[nodeIndex4_1.y + leaf4_z1.y],
        _VxShadowMapsBuffer[nodeIndex4_1.z + leaf4_z1.z],
        _VxShadowMapsBuffer[nodeIndex4_1.w + leaf4_z1.w]);

    uint4 bitmask04_0 = uint4(
        innerResults[0].y ? 0x00000000 : 0xFFFFFFFF,
        innerResults[1].y ? 0x00000000 : 0xFFFFFFFF,
        innerResults[2].y ? 0x00000000 : 0xFFFFFFFF,
        innerResults[3].y ? 0x00000000 : 0xFFFFFFFF);
    uint4 bitmask04_1 = uint4(
        innerResults[4].y ? 0x00000000 : 0xFFFFFFFF,
        innerResults[5].y ? 0x00000000 : 0xFFFFFFFF,
        innerResults[6].y ? 0x00000000 : 0xFFFFFFFF,
        innerResults[7].y ? 0x00000000 : 0xFFFFFFFF);
    uint4 bitmask14_0 = bitmask04_0;
    uint4 bitmask14_1 = bitmask04_1;

    if (innerResults[0].w)
    {
        bitmask04_0.x = _VxShadowMapsBuffer[leafIndex_0.x];
        bitmask14_0.x = _VxShadowMapsBuffer[leafIndex_0.x + 1];
    }
    if (innerResults[1].w)
    {
        bitmask04_0.y = _VxShadowMapsBuffer[leafIndex_0.y];
        bitmask14_0.y = _VxShadowMapsBuffer[leafIndex_0.y + 1];
    }
    if (innerResults[2].w)
    {
        bitmask04_0.z = _VxShadowMapsBuffer[leafIndex_0.z];
        bitmask14_0.z = _VxShadowMapsBuffer[leafIndex_0.z + 1];
    }
    if (innerResults[3].w)
    {
        bitmask04_0.w = _VxShadowMapsBuffer[leafIndex_0.w];
        bitmask14_0.w = _VxShadowMapsBuffer[leafIndex_0.w + 1];
    }
    if (innerResults[4].w)
    {
        bitmask04_1.x = _VxShadowMapsBuffer[leafIndex_1.x];
        bitmask14_1.x = _VxShadowMapsBuffer[leafIndex_1.x + 1];
    }
    if (innerResults[5].w)
    {
        bitmask04_1.y = _VxShadowMapsBuffer[leafIndex_1.y];
        bitmask14_1.y = _VxShadowMapsBuffer[leafIndex_1.y + 1];
    }
    if (innerResults[6].w)
    {
        bitmask04_1.z = _VxShadowMapsBuffer[leafIndex_1.z];
        bitmask14_1.z = _VxShadowMapsBuffer[leafIndex_1.z + 1];
    }
    if (innerResults[7].w)
    {
        bitmask04_1.w = _VxShadowMapsBuffer[leafIndex_1.w];
        bitmask14_1.w = _VxShadowMapsBuffer[leafIndex_1.w + 1];
    }

    uint4 bitmask4_0 = leaf4_y0 < 4 ? bitmask04_0 : bitmask14_0;
    uint4 bitmask4_1 = leaf4_y1 < 4 ? bitmask04_1 : bitmask14_1;

    uint4 maskShift4_0 = leaf4_x0 + 8 * (leaf4_y0 % 4);
    uint4 maskShift4_1 = leaf4_x1 + 8 * (leaf4_y1 % 4);
    uint4 mask4_0 = uint4(1, 1, 1, 1) << maskShift4_0;
    uint4 mask4_1 = uint4(1, 1, 1, 1) << maskShift4_1;

    float4 attenuation4_0 = (bitmask4_0 & mask4_0) == 0 ? 1.0 : 0.0;
    float4 attenuation4_1 = (bitmask4_1 & mask4_1) == 0 ? 1.0 : 0.0;
    attenuation4_0.xy = lerp(attenuation4_0.xz, attenuation4_0.yw, lerpWeight.x);
    attenuation4_0.x  = lerp(attenuation4_0.x,  attenuation4_0.y,  lerpWeight.y);
    attenuation4_1.xy = lerp(attenuation4_1.xz, attenuation4_1.yw, lerpWeight.x);
    attenuation4_1.x  = lerp(attenuation4_1.x,  attenuation4_1.y,  lerpWeight.y);

    return lerp(attenuation4_0.x, attenuation4_1.x, lerpWeight.z);
}

uint MaskBitsetVxShadowsType(uint vxShadowsBitset)
{
    return vxShadowsBitset & 0xC0000000;
}

uint MaskBitsetVxShadowMapBegin(uint vxShadowsBitset)
{
    return vxShadowsBitset & 0x3FFFFFFF;
}

bool IsVxShadowsEnabled(uint vxShadowsBitset)
{
    return MaskBitsetVxShadowsType(vxShadowsBitset) != 0x00000000;
}

bool IsVxShadowsOnly(uint vxShadowsBitset)
{
    return MaskBitsetVxShadowsType(vxShadowsBitset) == VX_SHADOWS_ONLY;
}

float NearestSampleVxShadowing(uint begin, float3 positionWS)
{
    uint dagScale = _VxShadowMapsBuffer[begin + 2];
    uint voxelResolution = 1 << dagScale;
    float4x4 worldToShadowMatrix =
    {
        asfloat(_VxShadowMapsBuffer[begin +  3]),
        asfloat(_VxShadowMapsBuffer[begin +  4]),
        asfloat(_VxShadowMapsBuffer[begin +  5]),
        asfloat(_VxShadowMapsBuffer[begin +  6]),

        asfloat(_VxShadowMapsBuffer[begin +  7]),
        asfloat(_VxShadowMapsBuffer[begin +  8]),
        asfloat(_VxShadowMapsBuffer[begin +  9]),
        asfloat(_VxShadowMapsBuffer[begin + 10]),

        asfloat(_VxShadowMapsBuffer[begin + 11]),
        asfloat(_VxShadowMapsBuffer[begin + 12]),
        asfloat(_VxShadowMapsBuffer[begin + 13]),
        asfloat(_VxShadowMapsBuffer[begin + 14]),

        0.0, 0.0, 0.0, 1.0,
    };

    float3 posNDC = mul(worldToShadowMatrix, float4(positionWS, 1.0)).xyz;
    float3 posP = posNDC * (float)voxelResolution;
    uint3  posQ = (uint3)posP;

    if (any(posQ >= (voxelResolution.xxx - 1)))
        return 1;

    uint4 result;
    TraverseVxShadowMapPosQ(begin, OFFSET_DIR, posQ, result);

    if (result.w == 0)
        return result.y ? 1 : 0;

    float attenuation = TraverseNearestSampleVxShadowMap(begin, OFFSET_DIR, posQ, result);

    return attenuation;
}

#endif // UNITY_VX_SHADOWMAPS_COMMON_INCLUDED
