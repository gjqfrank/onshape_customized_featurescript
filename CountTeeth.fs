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

// 在周期性序列中统计越过阈值的上升沿数量(=齿数)
function countPeaksAbove(samples is array, threshold is ValueWithUnits) returns number
{
    var n = size(samples);
    if (n == 0)
        return 0;
    var count = 0;
    for (var i = 0; i < n; i += 1)
    {
        var prev = samples[(i - 1 + n) % n];
        var cur = samples[i];
        if (prev < threshold && cur >= threshold)
            count += 1;
    }
    return count;
}

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
        // 遍历所有边, 收集 (角度, 半径) 对到普通数组
        // (append 在FS中可靠工作, 不用box/bucket避免数组原地修改问题)
        var allEdges = evaluateQuery(context, qOwnedByBody(definition.part, EntityType.EDGE));

        if (size(allEdges) == 0)
            throw "No edges found, cannot sample.";

        var refU;
        if (abs(axisDir[0]) < 0.9)
            refU = normalize(cross(axisDir, vector(1, 0, 0)));
        else
            refU = normalize(cross(axisDir, vector(0, 1, 0)));
        var refV = cross(axisDir, refU);

        // 每边采样数: 总采样约720点(2点/度, 足够数齿)
        var samplesPerEdge = floor(720 / size(allEdges));
        if (samplesPerEdge < 2)
            samplesPerEdge = 2;

        // 收集所有采样点: {angle, radius}
        var sampleData = [];
        var sampleCount = 0;
        for (var edge in allEdges)
        {
            for (var i = 0; i < samplesPerEdge; i += 1)
            {
                var t = (i + 0.5) / samplesPerEdge;
                try silent
                {
                    var tl = evEdgeTangentLine(context, { "edge" : edge, "parameter" : t });
                    var pt = tl.origin;
                    var r = radialDistance(pt, axisOrigin, axisDir);
                    var d = pt - axisOrigin;
                    var proj = d - dot(d, axisDir) * axisDir;
                    var angle = atan2(dot(proj, refV), dot(proj, refU));
                    if (angle < 0)
                        angle += 2 * PI;
                    sampleData = append(sampleData, { "angle" : angle, "radius" : r });
                    sampleCount += 1;
                }
            }
        }

        if (sampleCount < 4)
            throw "Sampling failed. Samples: " ~ toString(sampleCount) ~ " / Edges: " ~ toString(size(allEdges));

        // ---------- 5. 按角度排序, 提取半径序列 ------------------------------
        // 选择排序(sampleCount <= 720, O(n^2)可接受)
        for (var i = 0; i < size(sampleData) - 1; i += 1)
        {
            var minIdx = i;
            for (var j = i + 1; j < size(sampleData); j += 1)
            {
                if (sampleData[j].angle < sampleData[minIdx].angle)
                    minIdx = j;
            }
            if (minIdx != i)
            {
                var tmp = sampleData[i];
                sampleData[i] = sampleData[minIdx];
                sampleData[minIdx] = tmp;
            }
        }

        var samples = [];
        for (var sd in sampleData)
            samples = append(samples, sd.radius);

        var rMax = 0 * meter;
        var rMin = 1e10 * meter;
        for (var s in samples)
        {
            if (s > rMax) rMax = s;
            if (s < rMin) rMin = s;
        }

        if (rMax <= 0 * meter)
            throw "All sample radii are zero. Samples: " ~ toString(sampleCount);

        var toothHeight = rMax - rMin;
        var tipThreshold = rMin + toothHeight * 0.7;
        var teeth = (toothHeight / rMax < 0.005) ? 0 : countPeaksAbove(samples, tipThreshold);

        // 红色标出齿顶采样点
        if (teeth > 0)
        {
            for (var sd in sampleData)
            {
                if (sd.radius >= tipThreshold)
                {
                    try silent
                    {
                        var pt = centerOnAxis
                            + refU * (cos(sd.angle) * sd.radius)
                            + refV * (sin(sd.angle) * sd.radius);
                        debug(context, pt, DebugColor.RED);
                    }
                }
            }
        }

        // ---------- 6. 输出 -------------------------------------------------
        var diagMsg = "Teeth: " ~ toString(teeth)
            ~ " | Tip R: " ~ toString(rMax)
            ~ " | Root R: " ~ toString(rMin)
            ~ " | Height: " ~ toString(toothHeight)
            ~ " | Cyl axes: " ~ toString(size(axes))
            ~ " | Concentric: " ~ toString(bestCount)
            ~ " | Edges: " ~ toString(size(allEdges));

        reportFeatureInfo(context, id, diagMsg);
        println(diagMsg);

        if (definition.rename && teeth > 0)
            setProperty(context, { "entities" : definition.part, "propertyName" : PropertyType.NAME, "value" : definition.namePrefix ~ " (" ~ toString(teeth) ~ "T)" });
    });
