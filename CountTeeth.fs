FeatureScript 3044;

import(path : "onshape/std/geometry.fs", version : "3044.0");

// ===========================================================================
// Count Teeth —— 识别带轮(pulley) / 链轮(sprocket) / 齿轮(gear) 的齿数
//
// 新方案(只需选一个 part):
//   1. 自动找回转轴: 找所有圆柱面, 聚类同心轴, 选包含最多圆柱面的轴
//      (回转体上有多个同心圆柱面: 内孔、轮毂、齿根圆等)
//   2. 几何中心: bbox 中心投影到轴上
//   3. 标识轴(黄线)和中心(黄点)
//   4. 全局径向采样: 遍历所有边, 按角度分360桶, 每桶取最大半径
//      → "角度-最大半径"曲线的周期性峰值 = 齿数
//      (不依赖边环, 不受减重孔/倒圆干扰, 因为齿顶半径永远最大)
// ===========================================================================

// 点到轴线的径向距离
function radialDistance(point is Vector, axisOrigin is Vector, axisDir is Vector) returns ValueWithUnits
{
    var d = point - axisOrigin;
    return norm(d - dot(d, axisDir) * axisDir);
}

// 点到轴线的垂直距离(用于判断两轴是否同心)
function pointToAxisDistance(point is Vector, axisOrigin is Vector, axisDir is Vector) returns ValueWithUnits
{
    var d = point - axisOrigin;
    return norm(cross(d, axisDir));
}

// (齿数由径向剖面的滞回阈值上升沿计数决定; 顶点仅用于红点可视化)

annotation { "Feature Type Name" : "Count Teeth", "Feature Type Description" : "Count teeth of a pulley or sprocket" }
export const countTeeth = defineFeature(function(context is Context, id is Id, definition is map)
    precondition
    {
        annotation { "Name" : "Pulley or sprocket part", "Filter" : EntityType.BODY && BodyType.SOLID, "MaxNumberOfPicks" : 1 }
        definition.part is Query;

        annotation { "Name" : "Rename part using tooth count", "Default" : false }
        definition.rename is boolean;

        annotation { "Name" : "Name prefix", "Default" : "Pulley" }
        definition.namePrefix is string;
    }
    {
        // ---------- 1. 自动找回转轴 -----------------------------------------
        // 找所有面, 尝试 evAxis 找圆柱面, 收集时去重(方向平行+原点共线视为同一个)
        var allFaces = evaluateQuery(context, qOwnedByBody(definition.part, EntityType.FACE));

        var axes = []; // 去重后的轴列表
        var counts = []; // 每根轴出现的次数
        var tol = 0.01 * meter; // 1cm 同心容差

        for (var face in allFaces)
        {
            try silent
            {
                var ax = evAxis(context, { "axis" : face });
                // 检查是否已有近似轴
                var found = false;
                for (var k = 0; k < size(axes); k += 1)
                {
                    if (abs(dot(axes[k].direction, ax.direction)) > 0.999 &&
                        pointToAxisDistance(axes[k].origin, ax.origin, ax.direction) < tol)
                    {
                        counts[k] += 1;
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    axes = append(axes, ax);
                    counts = append(counts, 1);
                }
            }
        }

        if (size(axes) == 0)
            throw "No cylindrical face found, cannot auto-detect rotation axis.";

        // 选出现次数最多的轴(回转轴上有最多同心圆柱面: 内孔、轮毂、齿根圆等)
        var bestCount = counts[0];
        var bestAxis = axes[0];
        for (var k = 1; k < size(axes); k += 1)
        {
            if (counts[k] > bestCount)
            {
                bestCount = counts[k];
                bestAxis = axes[k];
            }
        }

        var axisOrigin is Vector = bestAxis.origin;
        var axisDir is Vector = bestAxis.direction;

        // ---------- 2. 几何中心 ---------------------------------------------
        var bbox = evBox3d(context, { "topology" : definition.part });
        var bboxCenter = (bbox.minCorner + bbox.maxCorner) / 2;
        // 修正轴 origin 的径向坐标: 用 bbox 中心的径向坐标替代
        // evAxis 返回的圆柱面轴 origin 可能偏离真正回转中心(如齿槽圆弧面),
        // 对称零件 bbox 中心 ≈ 回转中心, 更稳健
        var radialOffset = (bboxCenter - axisOrigin) - dot(bboxCenter - axisOrigin, axisDir) * axisDir;
        var center = axisOrigin + radialOffset; // 径向(XZ)=bboxCenter, 轴向(Y)=axisOrigin

        // ---------- 4. 全局径向采样 -----------------------------------------
        var allEdges = evaluateQuery(context, qOwnedByBody(definition.part, EntityType.EDGE));

        if (size(allEdges) == 0)
            throw "No edges found, cannot sample.";

        var refU;
        if (abs(axisDir[0]) < 0.9)
            refU = normalize(cross(axisDir, vector(1, 0, 0)));
        else
            refU = normalize(cross(axisDir, vector(0, 1, 0)));
        var refV = cross(axisDir, refU);

        // 每边采样数: 总采样约2880点, 每边最少16点(提高角度分辨率, 减少空桶)
        var samplesPerEdge = floor(2880 / size(allEdges));
        if (samplesPerEdge < 16)
            samplesPerEdge = 16;

        // 第一遍: 收集所有采样点, 找全局最大半径
        var allSampleData = [];
        var globalMaxR = 0 * meter;
        for (var edge in allEdges)
        {
            for (var i = 0; i < samplesPerEdge; i += 1)
            {
                var t = (i + 0.5) / samplesPerEdge;
                try silent
                {
                    var pt = evEdgeTangentLine(context, { "edge" : edge, "parameter" : t }).origin;
                    var r = radialDistance(pt, center, axisDir);
                    var d = pt - center;
                    var proj = d - dot(d, axisDir) * axisDir;
                    var angle = atan2(dot(proj, refV), dot(proj, refU));
                    if (angle < 0)
                        angle += 2 * PI;
                    allSampleData = append(allSampleData, { "angle" : angle, "radius" : r });
                    if (r > globalMaxR) globalMaxR = r;
                }
            }
        }

        if (size(allSampleData) < 4)
            throw "Sampling failed. Samples: " ~ toString(size(allSampleData)) ~ " / Edges: " ~ toString(size(allEdges));

        // ---------- 5. 齿尖顶点聚类数齿(主方法) -----------------------------
        // 红点(齿尖顶点)位置准, 但每齿可能有1~4个顶点(平顶齿两角+圆角).
        // pulley注意: 上下有凸缘(flange), 比齿顶高, 凸缘顶点半径更大.
        //   策略: 不能直接取全局maxR(会被凸缘占据). 改用半径直方图找"齿顶圆"半径:
        //   把顶点按半径分桶, 找顶点数最多的桶(齿尖顶点最多), 该桶半径=tipCircleR.
        //   凸缘顶点数少(每圈凸缘只有少量顶点), 不会主导直方图.
        var allVertices = evaluateQuery(context, qOwnedByBody(definition.part, EntityType.VERTEX));
        var vertexPoints = [];
        var allAxials = [];
        for (var v in allVertices)
        {
            try silent
            {
                var pt = evVertexPoint(context, { "vertex" : v });
                var d = pt - center;
                var r = norm(d - dot(d, axisDir) * axisDir);
                var ax = dot(d, axisDir); // 相对中位面的轴向位置
                vertexPoints = append(vertexPoints, { "point" : pt, "radius" : r, "axial" : ax });
                allAxials = append(allAxials, ax);
            }
        }

        // 修正 center 的轴向坐标: 用顶点 axial 的中位数 = 零件轴向中位面
        // (axisOrigin 的轴向位置可能偏离零件中心, 导致对称点 ax' ≠ -ax)
        var sortedAxials = sort(allAxials, function(a, b) { return (a - b) / meter; });
        var axialMid = sortedAxials[floor(size(sortedAxials) / 2)];
        center = center + axialMid * axisDir; // 把中位面移到 ax=0
        // 重算所有顶点的 axial (相对新中位面)
        for (var i = 0; i < size(vertexPoints); i += 1)
            vertexPoints[i].axial = vertexPoints[i].axial - axialMid;

        // ---------- 3. 标识轴和中心 -----------------------------------------
        // 黄色画轴(用线)和中心点 (center 已修正为径向bbox+轴向中位数)
        try silent { debug(context, line(center, axisDir), DebugColor.YELLOW); }
        debug(context, center, DebugColor.YELLOW);

        // 找全局maxR(含凸缘)和minR, 用于直方图范围
        var globalMaxVR = 0 * meter;
        var minVR = vertexPoints[0].radius;
        for (var vp in vertexPoints)
        {
            if (vp.radius > globalMaxVR) globalMaxVR = vp.radius;
            if (vp.radius < minVR) minVR = vp.radius;
        }

        // 半径直方图: 分50桶, 对每个桶统计顶点覆盖了多少个不同的角度桶(36个角度桶, 每10度)
        // 齿尖顶点覆盖全圈(角度覆盖~36), 凸缘顶点覆盖少(凸缘是连续环但顶点少)
        var NBINS = 50;
        var NANGLE = 36;
        var rRange = globalMaxVR - minVR;
        if (rRange == 0 * meter) rRange = 1 * meter;
        var bucketAngleCover = []; // 每个半径桶的角度覆盖数
        var bucketAngleSet = []; // 每个半径桶的角度集合(用二维数组)
        for (var i = 0; i < NBINS; i += 1)
        {
            bucketAngleCover = append(bucketAngleCover, 0);
            var aSet = [];
            for (var j = 0; j < NANGLE; j += 1) aSet = append(aSet, false);
            bucketAngleSet = append(bucketAngleSet, aSet);
        }
        for (var vp in vertexPoints)
        {
            var frac = (vp.radius - minVR) / rRange;
            if (frac < 0) frac = 0;
            if (frac > 0.999) frac = 0.999;
            var bIdx = floor(frac * NBINS);
            var binR = minVR + rRange * (bIdx + 0.5) / NBINS;
            if (binR > globalMaxVR * 0.5)
            {
                var d = vp.point - center;
                var proj = d - dot(d, axisDir) * axisDir;
                var angle = atan2(dot(proj, refV), dot(proj, refU)) / degree;
                if (angle < 0) angle += 360;
                var aIdx = floor(angle / 360 * NANGLE) % NANGLE;
                if (!bucketAngleSet[bIdx][aIdx])
                {
                    bucketAngleSet[bIdx][aIdx] = true;
                    bucketAngleCover[bIdx] += 1;
                }
            }
        }
        // 选角度覆盖最广的桶 = 齿顶圆半径(齿尖顶点覆盖全圈)
        var bestBin = 0;
        var bestCover = 0;
        for (var i = 0; i < NBINS; i += 1)
        {
            if (bucketAngleCover[i] > bestCover)
            {
                bestCover = bucketAngleCover[i];
                bestBin = i;
            }
        }
        var tipCircleR = minVR + rRange * (bestBin + 0.5) / NBINS;

        // 齿尖顶点: radius在tipCircleR附近(±2%全局maxR)
        var tipTol = globalMaxVR * 0.02;
        var tipLo = tipCircleR - tipTol;
        var tipHi = tipCircleR + tipTol;
        var tipVertexCount = 0;
        var tipPts = []; // 收集齿尖点(带角度)
        var tipAngles = []; // 角度(度)
        for (var vp in vertexPoints)
        {
            if (vp.radius >= tipLo && vp.radius <= tipHi)
            {
                var d = vp.point - center;
                var proj = d - dot(d, axisDir) * axisDir;
                var angle = atan2(dot(proj, refV), dot(proj, refU)) / degree;
                if (angle < 0)
                    angle += 360;
                tipAngles = append(tipAngles, angle);
                tipPts = append(tipPts, { "angle" : angle, "point" : vp.point, "axial" : vp.axial });
            }
        }

        if (size(tipAngles) < 3)
            throw "Too few tip vertices (" ~ toString(size(tipAngles)) ~ ") for clustering. tipCircleR: " ~ toString(tipCircleR);

        // 按角度排序
        tipAngles = sort(tipAngles, function(a, b) { return a - b; });
        tipPts = sort(tipPts, function(a, b) { return a.angle - b.angle; });

        // 计算所有相邻角度差(含wrap-around)
        var gaps = [];
        for (var i = 1; i < size(tipAngles); i += 1)
            gaps = append(gaps, tipAngles[i] - tipAngles[i - 1]);
        // wrap gap: 从最后一个到第一个(跨360)
        var wrapGap = (360 - tipAngles[size(tipAngles) - 1]) + tipAngles[0];
        gaps = append(gaps, wrapGap);

        // 过滤孤立顶点(字样/噪点): 计算每个顶点到最近邻居的角度距离,
        // 如果某顶点的最近邻居距离 > 平均距离的3倍, 认为是孤立点, 移除.
        // 真齿尖顶点有邻居(同齿其他顶点或相邻齿), 字样顶点孤立.
        var nnDists = [];
        for (var i = 0; i < size(gaps); i += 1)
        {
            var prevGap = gaps[(i - 1 + size(gaps)) % size(gaps)];
            var nextGap = gaps[i];
            var nnDist = min(prevGap, nextGap);
            nnDists = append(nnDists, nnDist);
        }
        var nnSum = 0;
        for (var d in nnDists) nnSum += d;
        var nnAvg = nnSum / size(nnDists);
        var nnThresh = nnAvg * 3;
        var filteredAngles = [];
        var filteredTipPts = [];
        for (var i = 0; i < size(tipAngles); i += 1)
        {
            if (nnDists[i] <= nnThresh)
            {
                filteredAngles = append(filteredAngles, tipAngles[i]);
                filteredTipPts = append(filteredTipPts, tipPts[i]);
            }
        }
        tipAngles = filteredAngles;
        tipPts = filteredTipPts;
        tipVertexCount = size(tipAngles);

        // 重新计算gaps(过滤后)
        gaps = [];
        for (var i = 1; i < size(tipAngles); i += 1)
            gaps = append(gaps, tipAngles[i] - tipAngles[i - 1]);
        wrapGap = (360 - tipAngles[size(tipAngles) - 1]) + tipAngles[0];
        gaps = append(gaps, wrapGap);

        if (tipVertexCount < 3)
            throw "Too few tip vertices after isolation filter (" ~ toString(tipVertexCount) ~ ").";

        // 红点标出在分层滤波后绘制 (见下方)

        // 聚类数齿: 用最大gap估计齿距, 再按齿距的一半聚类
        // maxGap是最大的齿间gap, 360/maxGap给出齿数下界估计
        // 阈值 = 估计齿距 * 0.5 (同齿内gap < 半齿距, 齿间gap > 半齿距)
        var sortedGaps = sort(gaps, function(a, b) { return a - b; });
        var maxGap = sortedGaps[size(sortedGaps) - 1];
        var estTeeth = floor(360 / maxGap + 0.5);
        if (estTeeth < 1) estTeeth = 1;
        var estPitch = 360 / estTeeth;
        var clusterThresh = estPitch * 0.5;

        // 数gap > 阈值的次数 = 齿间分隔数 = 齿数
        var teeth = 0;
        for (var g in gaps)
        {
            if (g > clusterThresh)
                teeth += 1;
        }
        if (teeth < 1)
            teeth = 1;

        // ---------- 5c. 轴向分层滤波 ---------------------------------------
        // 第一次算出 teeth 后, 检验每个水平截面(按axial分组)的红点数.
        // 若某截面点数 < teeth (说明该截面不是真正的齿尖层, 如字样/凸缘),
        // 过滤掉该截面所有点, 用剩余点重新计算齿数.
        var layerFilterApplied = false;
        if (teeth >= 6 && size(tipPts) > teeth * 2)
        {
            // 按axial聚类分层 (容差 0.5mm)
            var layerTol = 0.0005 * meter;
            var layers = []; // 每层 = { axial, pts: [] }
            for (var p in tipPts)
            {
                var foundLayer = false;
                for (var L = 0; L < size(layers); L += 1)
                {
                    if (abs(layers[L].axial - p.axial) < layerTol)
                    {
                        layers[L].pts = append(layers[L].pts, p);
                        foundLayer = true;
                        break;
                    }
                }
                if (!foundLayer)
                    layers = append(layers, { "axial" : p.axial, "pts" : [p] });
            }

            // 过滤掉点数 < teeth 的层 (这些层不是真正的齿尖层)
            var filteredTipPts2 = [];
            for (var L = 0; L < size(layers); L += 1)
            {
                if (size(layers[L].pts) >= teeth)
                {
                    for (var p in layers[L].pts)
                        filteredTipPts2 = append(filteredTipPts2, p);
                }
            }

            // 如果过滤后点数变化且仍 >= 3, 重新计算齿数
            if (size(filteredTipPts2) >= 3 && size(filteredTipPts2) < size(tipPts))
            {
                tipPts = sort(filteredTipPts2, function(a, b) { return a.angle - b.angle; });
                tipAngles = [];
                for (var p in tipPts)
                    tipAngles = append(tipAngles, p.angle);
                tipVertexCount = size(tipAngles);

                gaps = [];
                for (var i = 1; i < size(tipAngles); i += 1)
                    gaps = append(gaps, tipAngles[i] - tipAngles[i - 1]);
                wrapGap = (360 - tipAngles[size(tipAngles) - 1]) + tipAngles[0];
                gaps = append(gaps, wrapGap);

                sortedGaps = sort(gaps, function(a, b) { return a - b; });
                maxGap = sortedGaps[size(sortedGaps) - 1];
                estTeeth = floor(360 / maxGap + 0.5);
                if (estTeeth < 1) estTeeth = 1;
                estPitch = 360 / estTeeth;
                clusterThresh = estPitch * 0.5;

                teeth = 0;
                for (var g in gaps)
                {
                    if (g > clusterThresh)
                        teeth += 1;
                }
                if (teeth < 1)
                    teeth = 1;
                layerFilterApplied = true;
            }
        }

        // 红点标出最终的齿尖顶点 (分层滤波后)
        for (var p in tipPts)
            debug(context, p.point, DebugColor.RED);

        // 诊断: gap统计
        var smallGapCount = 0;
        var bigGapCount = 0;
        var bigGapSum = 0;
        var smallGapSum = 0;
        for (var g in gaps)
        {
            if (g > clusterThresh)
            {
                bigGapCount += 1;
                bigGapSum += g;
            }
            else
            {
                smallGapCount += 1;
                smallGapSum += g;
            }
        }
        var bigGapAvg = 0;
        if (bigGapCount > 0)
            bigGapAvg = bigGapSum / bigGapCount;
        var smallGapAvg = 0;
        if (smallGapCount > 0)
            smallGapAvg = smallGapSum / smallGapCount;

        // 修正: 矩形不共面排列时, 每齿4顶点对称分布, 齿间和齿内各2个gap,
        // small gap数 == big gap数, 且 big≈small (都是半齿距), 导致齿数翻倍.
        // 线段(2顶点/齿)虽然 small==big, 但 small≈0, big≈齿距, 比值很大.
        // 用 big/small 比值 < 2 区分矩形(比值≈1)和线段(比值>>1).
        if (bigGapCount > 0 && smallGapCount > 0 && smallGapAvg > 0.01 &&
            abs(bigGapCount - smallGapCount) <= max(1, floor(bigGapCount * 0.1)) &&
            bigGapAvg / smallGapAvg < 2)
        {
            teeth = floor(teeth / 2);
            if (teeth < 1) teeth = 1;
        }

        // 校验: 大gap平均值应接近 360/teeth
        var expectedGap = 360 / teeth;
        var bigGaps = bigGapCount;
        var thresh = clusterThresh;

        // ---------- 6. 输出 -------------------------------------------------
        var diagMsg = "Teeth: " ~ toString(teeth)
            ~ " | Tip vertices: " ~ toString(tipVertexCount)
            ~ " | TipCircleR: " ~ toString(tipCircleR)
            ~ " | AngleCover: " ~ toString(bestCover) ~ "/" ~ toString(NANGLE)
            ~ " | GlobalMaxVR: " ~ toString(globalMaxVR)
            ~ " | BigGaps: " ~ toString(bigGaps)
            ~ " | SmallGaps: " ~ toString(smallGapCount)
            ~ " | Thresh: " ~ toString(round(thresh * 10) / 10)
            ~ " | BigGapAvg: " ~ toString(round(bigGapAvg * 10) / 10)
            ~ " | SmallGapAvg: " ~ toString(round(smallGapAvg * 10) / 10)
            ~ " | ExpectedGap: " ~ toString(round(expectedGap * 10) / 10)
            ~ " | Center: " ~ toString(center)
            ~ " | LayerFiltered: " ~ toString(layerFilterApplied)
            ~ " | Edges: " ~ toString(size(allEdges));

        reportFeatureInfo(context, id, diagMsg);
        println(diagMsg);

        if (definition.rename && teeth > 0)
            setProperty(context, { "entities" : definition.part, "propertyName" : PropertyType.NAME, "value" : definition.namePrefix ~ " (" ~ toString(teeth) ~ "T)" });
    });
