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
        // 投影到轴上
        var centerOnAxis = axisOrigin + dot(bboxCenter - axisOrigin, axisDir) * axisDir;

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
                    var r = radialDistance(pt, axisOrigin, axisDir);
                    var d = pt - axisOrigin;
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
        // pulley注意: 上下有凸缘(flange), 比齿顶高. 必须先过滤掉凸缘顶点.
        // 策略: 收集所有顶点的(角度,半径,轴向位置), 按轴向位置排序,
        //   去掉两端各10%的顶点(凸缘区), 用中间80%找maxR和齿尖.
        //   对gear(无凸缘)也适用: 去掉两端10%不影响齿尖(齿尖在中间).
        var tipR = globalMaxR;

        var allVertices = evaluateQuery(context, qOwnedByBody(definition.part, EntityType.VERTEX));
        var vertexPoints = [];
        for (var v in allVertices)
        {
            try silent
            {
                var pt = evVertexPoint(context, { "vertex" : v });
                var r = radialDistance(pt, axisOrigin, axisDir);
                var axialPos = dot(pt - axisOrigin, axisDir);
                vertexPoints = append(vertexPoints, { "point" : pt, "radius" : r, "axialPos" : axialPos });
            }
        }

        // 按轴向位置排序, 找中间80%的轴向范围(去掉两端各10%, 排除凸缘)
        var sortedByAxial = sort(vertexPoints, function(a, b) { return (a.axialPos - b.axialPos) / meter; });
        var nVerts = size(sortedByAxial);
        var trimCount = floor(nVerts * 0.10);
        var axialLo = sortedByAxial[trimCount].axialPos;
        var axialHi = sortedByAxial[nVerts - 1 - trimCount].axialPos;

        // 在中间轴向范围内找最大半径(齿顶圆, 排除凸缘)
        var vertexMaxR = 0 * meter;
        for (var vp in vertexPoints)
        {
            if (vp.axialPos >= axialLo && vp.axialPos <= axialHi && vp.radius > vertexMaxR)
                vertexMaxR = vp.radius;
        }

        // 齿尖顶点: 在中间轴向范围 且 radius > 98% vertexMaxR
        var tipVertexCount = 0;
        var tipAngles = []; // 角度(度)
        for (var vp in vertexPoints)
        {
            if (vp.axialPos >= axialLo && vp.axialPos <= axialHi && vp.radius > vertexMaxR * 0.98)
            {
                debug(context, vp.point, DebugColor.RED);
                tipVertexCount += 1;
                var d = vp.point - axisOrigin;
                var proj = d - dot(d, axisDir) * axisDir;
                var angle = atan2(dot(proj, refV), dot(proj, refU)) / degree;
                if (angle < 0)
                    angle += 360;
                tipAngles = append(tipAngles, angle);
            }
        }

        if (tipVertexCount < 3)
            throw "Too few tip vertices (" ~ toString(tipVertexCount) ~ ") for clustering.";

        // 按角度排序
        tipAngles = sort(tipAngles, function(a, b) { return a - b; });

        // 计算所有相邻角度差(含wrap-around)
        var gaps = [];
        for (var i = 1; i < size(tipAngles); i += 1)
            gaps = append(gaps, tipAngles[i] - tipAngles[i - 1]);
        // wrap gap: 从最后一个到第一个(跨360)
        var wrapGap = (360 - tipAngles[size(tipAngles) - 1]) + tipAngles[0];
        gaps = append(gaps, wrapGap);

        // 双峰gap分离(Otsu式): 找一个阈值把gaps分成"小gap(同齿内)"和"大gap(齿间)"两类
        // 使类内方差最小(等价于类间方差最大).
        // 比中位数稳健: 中位数在顶点分布不均时会落在错误位置.
        var sortedGaps = sort(gaps, function(a, b) { return a - b; });
        var minGap = sortedGaps[0];
        var maxGap = sortedGaps[size(sortedGaps) - 1];
        var clusterThresh = (minGap + maxGap) / 2;
        var bestVar = -1;
        for (var t = 0; t < size(sortedGaps) - 1; t += 1)
        {
            var thresh = (sortedGaps[t] + sortedGaps[t + 1]) / 2;
            var n1 = t + 1;
            var n2 = size(sortedGaps) - n1;
            if (n1 == 0 || n2 == 0) continue;
            var sum1 = 0;
            for (var i = 0; i <= t; i += 1) sum1 += sortedGaps[i];
            var mean1 = sum1 / n1;
            var sum2 = 0;
            for (var i = t + 1; i < size(sortedGaps); i += 1) sum2 += sortedGaps[i];
            var mean2 = sum2 / n2;
            var w = n1 * n2 * (mean1 - mean2) * (mean1 - mean2);
            if (w > bestVar)
            {
                bestVar = w;
                clusterThresh = thresh;
            }
        }
        var medianGap = sortedGaps[size(sortedGaps) / 2];

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
        var coveredBins = tipVertexCount;
        var rawHighRegions = teeth;
        var bigGaps = bigGapCount;
        var highBinCount = tipVertexCount;
        var dedendumR = 0 * meter;
        var thresh = clusterThresh;
        var amplitude = 0 * meter;
        var rootR = 0 * meter;

        // ---------- 6. 输出 -------------------------------------------------
        var diagMsg = "Teeth: " ~ toString(teeth)
            ~ " | Tip vertices: " ~ toString(tipVertexCount)
            ~ " | BigGaps: " ~ toString(bigGaps)
            ~ " | SmallGaps: " ~ toString(smallGapCount)
            ~ " | Thresh: " ~ toString(round(thresh * 10) / 10)
            ~ " | BigGapAvg: " ~ toString(round(bigGapAvg * 10) / 10)
            ~ " | ExpectedGap: " ~ toString(round(expectedGap * 10) / 10)
            ~ " | MinGap: " ~ toString(round(minGap * 10) / 10)
            ~ " | MaxGap: " ~ toString(round(maxGap * 10) / 10)
            ~ " | Tip R: " ~ toString(tipR)
            ~ " | Edges: " ~ toString(size(allEdges));

        reportFeatureInfo(context, id, diagMsg);
        println(diagMsg);

        if (definition.rename && teeth > 0)
            setProperty(context, { "entities" : definition.part, "propertyName" : PropertyType.NAME, "value" : definition.namePrefix ~ " (" ~ toString(teeth) ~ "T)" });
    });
