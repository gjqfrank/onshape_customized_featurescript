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
        var centerOnAxis = center + dot(bboxCenter - center, axisDir) * axisDir;

        // ---------- 3. 标识轴和中心 -----------------------------------------
        // 黄色画轴(用线)和中心点
        try silent { debug(context, line(centerOnAxis, axisDir), DebugColor.YELLOW); }
        debug(context, centerOnAxis, DebugColor.YELLOW);

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
        for (var v in allVertices)
        {
            try silent
            {
                var pt = evVertexPoint(context, { "vertex" : v });
                var d = pt - center;
                var r = norm(d - dot(d, axisDir) * axisDir);
                var ax = dot(d, axisDir); // 相对中位面的轴向位置
                vertexPoints = append(vertexPoints, { "point" : pt, "radius" : r, "axial" : ax });
            }
        }

        // 找全局maxR(含凸缘)和minR, 用于直方图范围
        var globalMaxVR = 0 * meter;
        var minVR = vertexPoints[0].radius;
        for (var vp in vertexPoints)
        {
            if (vp.radius > globalMaxVR) globalMaxVR = vp.radius;
            if (vp.radius < minVR) minVR = vp.radius;
        }

        // 半径直方图: 分50桶, 找顶点数最多的桶 = 齿顶圆半径
        var NBINS = 50;
        var rHisto = [];
        for (var i = 0; i < NBINS; i += 1)
            rHisto = append(rHisto, 0);
        var rRange = globalMaxVR - minVR;
        if (rRange == 0 * meter) rRange = 1 * meter;
        for (var vp in vertexPoints)
        {
            var frac = (vp.radius - minVR) / rRange;
            if (frac < 0) frac = 0;
            if (frac > 0.999) frac = 0.999;
            rHisto[floor(frac * NBINS)] += 1;
        }
        // 找齿顶圆半径: 从最大半径开始, 找第一个顶点数 >= 8 的桶
        // (齿尖是最大半径, 每齿至少1~2个顶点, 8T以上齿尖顶点数 >= 8;
        //  凸缘/字样顶点少 < 8, 不会误选)
        var bestBin = 0;
        for (var i = NBINS - 1; i >= 0; i -= 1)
        {
            var binR = minVR + rRange * (i + 0.5) / NBINS;
            if (binR > globalMaxVR * 0.5 && rHisto[i] >= 8)
            {
                bestBin = i;
                break;
            }
        }
        var tipCircleR = minVR + rRange * (bestBin + 0.5) / NBINS;

        // ---------- 5b. 边采样峰值计数(主方法) -----------------------------
        // 顶点聚类在平顶齿(每齿多顶点均匀分布)时会误判(如24T→48T).
        // 改用边采样的角度-半径曲线, 数"从低到高"上升沿 = 齿数.
        // 只保留半径 ≤ tipCircleR + cap的采样(排除凸缘), 按角度分桶取最大半径.
        var capR = tipCircleR + globalMaxVR * 0.02;
        var NBUCKETS = 360;
        var bucketMaxR = [];
        for (var i = 0; i < NBUCKETS; i += 1)
            bucketMaxR = append(bucketMaxR, 0 * meter);

        var sampleMinR = capR;
        for (var sd in allSampleData)
        {
            if (sd.radius > capR) continue;
            var bIdx = floor((sd.angle / (2 * PI) * NBUCKETS) / radian) % NBUCKETS;
            if (sd.radius > bucketMaxR[bIdx])
                bucketMaxR[bIdx] = sd.radius;
            if (sd.radius > 0 * meter && sd.radius < sampleMinR)
                sampleMinR = sd.radius;
        }

        // 填充空桶(线性插值)
        var firstNonZero = -1;
        for (var i = 0; i < NBUCKETS; i += 1)
        {
            if (bucketMaxR[i] > 0 * meter) { firstNonZero = i; break; }
        }
        if (firstNonZero >= 0)
        {
            var lastVal = bucketMaxR[firstNonZero];
            for (var i = 0; i < firstNonZero; i += 1)
                bucketMaxR[i] = lastVal;
            for (var i = firstNonZero + 1; i < NBUCKETS; i += 1)
            {
                if (bucketMaxR[i] > 0 * meter)
                    lastVal = bucketMaxR[i];
                else
                    bucketMaxR[i] = lastVal;
            }
        }

        var edgeTeeth = 0;
        if (firstNonZero >= 0 && sampleMinR < capR)
        {
            var highThresh = tipCircleR - globalMaxVR * 0.02;
            var lowThresh = (tipCircleR + sampleMinR) / 2;
            var inHigh = false;
            for (var i = 0; i < NBUCKETS; i += 1)
            {
                if (!inHigh && bucketMaxR[i] >= highThresh)
                {
                    inHigh = true;
                    edgeTeeth += 1;
                }
                else if (inHigh && bucketMaxR[i] < lowThresh)
                    inHigh = false;
            }
            // 首尾同齿修正: 如果首尾都在high, 减1
            if (edgeTeeth > 1 && bucketMaxR[0] >= highThresh && bucketMaxR[NBUCKETS - 1] >= highThresh)
                edgeTeeth -= 1;
        }

        // 齿尖顶点: radius在tipCircleR附近(收紧容差: 直方图桶宽的70%, 排除字样顶点)
        var tipTol = min(globalMaxVR * 0.02, rRange / NBINS * 0.7);
        var tipVertexCount = 0;
        // 先收集所有半径匹配的齿尖点(带角度+轴向)
        var tipPts = [];
        for (var vp in vertexPoints)
        {
            if (abs(vp.radius - tipCircleR) < tipTol)
            {
                var d = vp.point - center;
                var proj = d - dot(d, axisDir) * axisDir;
                var angle = atan2(dot(proj, refV), dot(proj, refU)) / degree;
                if (angle < 0)
                    angle += 360;
                tipPts = append(tipPts, { "angle" : angle, "axial" : vp.axial, "point" : vp.point });
            }
        }

        // 对称性筛选: 中位面在 ax=0, 每个点需在对称位置 ax'≈-ax (角度相同)有匹配点
        // 容差: 轴向 1mm, 角度 2度. 无对称匹配的点(字样/单侧凸缘)被排除.
        var axTolSym = 0.001 * meter;
        var angTolSym = 2.0;
        var symTipPts = [];
        for (var i = 0; i < size(tipPts); i += 1)
        {
            var p = tipPts[i];
            var hasSym = false;
            for (var j = 0; j < size(tipPts); j += 1)
            {
                if (j == i) continue;
                var q = tipPts[j];
                if (abs(q.axial + p.axial) < axTolSym && abs(q.angle - p.angle) < angTolSym)
                {
                    hasSym = true;
                    break;
                }
            }
            if (hasSym)
                symTipPts = append(symTipPts, p);
        }
        tipPts = symTipPts;

        if (size(tipPts) < 3)
            throw "Too few tip vertices (" ~ toString(size(tipPts)) ~ ") for clustering. tipCircleR: " ~ toString(tipCircleR);

        // 按角度排序 tipPts (保持 tipAngles 与 tipPts 索引一致)
        tipPts = sort(tipPts, function(a, b) { return a.angle - b.angle; });
        var tipAngles = []; // 角度(度)
        for (var p in tipPts)
            tipAngles = append(tipAngles, p.angle);

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
        for (var i = 0; i < size(tipAngles); i += 1)
        {
            if (nnDists[i] <= nnThresh)
                filteredAngles = append(filteredAngles, tipAngles[i]);
        }
        tipAngles = filteredAngles;
        // 同步过滤 tipPts (保留 isolation filter 通过的点)
        var filteredTipPts = [];
        for (var i = 0; i < size(tipPts); i += 1)
        {
            if (nnDists[i] <= nnThresh)
                filteredTipPts = append(filteredTipPts, tipPts[i]);
        }
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

        // 红点标出过滤后的齿尖顶点
        for (var p in tipPts)
            debug(context, p.point, DebugColor.RED);

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

        // 诊断: gap统计
        var smallGapCount = 0;
        var bigGapCount = 0;
        var bigGapSum = 0;
        for (var g in gaps)
        {
            if (g > clusterThresh)
            {
                bigGapCount += 1;
                bigGapSum += g;
            }
            else
                smallGapCount += 1;
        }
        var bigGapAvg = 0;
        if (bigGapCount > 0)
            bigGapAvg = bigGapSum / bigGapCount;

        // 校验: 大gap平均值应接近 360/teeth
        var expectedGap = 360 / teeth;
        var bigGaps = bigGapCount;
        var thresh = clusterThresh;

        // ---------- 5c. 选择最终齿数 ----------------------------------------
        // 优先用边采样峰值计数(对平顶齿更可靠), 回退到顶点聚类
        var vertexTeeth = teeth;
        if (edgeTeeth > 0)
            teeth = edgeTeeth;

        // ---------- 6. 输出 -------------------------------------------------
        var diagMsg = "Teeth: " ~ toString(teeth)
            ~ " | EdgeTeeth: " ~ toString(edgeTeeth)
            ~ " | VertexTeeth: " ~ toString(vertexTeeth)
            ~ " | Tip vertices: " ~ toString(tipVertexCount)
            ~ " | TipCircleR: " ~ toString(tipCircleR)
            ~ " | GlobalMaxVR: " ~ toString(globalMaxVR)
            ~ " | BigGaps: " ~ toString(bigGaps)
            ~ " | SmallGaps: " ~ toString(smallGapCount)
            ~ " | Thresh: " ~ toString(round(thresh * 10) / 10)
            ~ " | BigGapAvg: " ~ toString(round(bigGapAvg * 10) / 10)
            ~ " | ExpectedGap: " ~ toString(round(expectedGap * 10) / 10)
            ~ " | Center: " ~ toString(centerOnAxis)
            ~ " | Edges: " ~ toString(size(allEdges));

        reportFeatureInfo(context, id, diagMsg);
        println(diagMsg);

        if (definition.rename && teeth > 0)
            setProperty(context, { "entities" : definition.part, "propertyName" : PropertyType.NAME, "value" : definition.namePrefix ~ " (" ~ toString(teeth) ~ "T)" });
    });
