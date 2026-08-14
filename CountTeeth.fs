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
        var allEdges = evaluateQuery(context, qOwnedByBody(definition.part, EntityType.EDGE));

        if (size(allEdges) == 0)
            throw "No edges found, cannot sample.";

        var refU;
        if (abs(axisDir[0]) < 0.9)
            refU = normalize(cross(axisDir, vector(1, 0, 0)));
        else
            refU = normalize(cross(axisDir, vector(0, 1, 0)));
        var refV = cross(axisDir, refU);

        // 每边采样数: 总采样约1440点, 每边最少8点
        var samplesPerEdge = floor(1440 / size(allEdges));
        if (samplesPerEdge < 8)
            samplesPerEdge = 8;

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

        // 第二遍: 过滤非齿廓采样点(只保留半径 > 50% maxR, 排除减重孔/内孔)
        var sampleData = [];
        for (var sd in allSampleData)
        {
            if (sd.radius > globalMaxR * 0.5)
                sampleData = append(sampleData, sd);
        }

        if (size(sampleData) < 4)
            throw "No tooth-area samples after filtering. GlobalMaxR: " ~ toString(globalMaxR);

        // ---------- 5. 按角度排序, 提取半径序列 ------------------------------
        sampleData = sort(sampleData, function(a, b)
        {
            return a.angle - b.angle;
        });

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

        var toothHeight = rMax - rMin;
        var tipThreshold = rMin + toothHeight * 0.7;
        var teeth = (toothHeight / rMax < 0.005) ? 0 : countPeaksAbove(samples, tipThreshold);

        // ---------- 5b. 顶点计数(交叉验证) ----------------------------------
        // 每个齿尖通常有顶点, 数半径接近maxR的顶点
        var allVertices = evaluateQuery(context, qOwnedByBody(definition.part, EntityType.VERTEX));
        var vertexTeeth = 0;
        var vertexMaxR = 0 * meter;
        var vertexRadii = [];
        for (var v in allVertices)
        {
            try silent
            {
                var pt = evVertexPoint(context, { "vertex" : v });
                var r = radialDistance(pt, axisOrigin, axisDir);
                vertexRadii = append(vertexRadii, r);
                if (r > vertexMaxR) vertexMaxR = r;
            }
        }
        for (var r in vertexRadii)
        {
            if (r > vertexMaxR * 0.95)
                vertexTeeth += 1;
        }

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
        var angleMin = sampleData[0].angle;
        var angleMax = sampleData[0].angle;
        for (var sd in sampleData)
        {
            if (sd.angle < angleMin) angleMin = sd.angle;
            if (sd.angle > angleMax) angleMax = sd.angle;
        }
        var angleRangeDeg = (angleMax - angleMin) * 180 / PI;

        var diagMsg = "Teeth: " ~ toString(teeth)
            ~ " | VertexTeeth: " ~ toString(vertexTeeth)
            ~ " | Tip R: " ~ toString(rMax)
            ~ " | Root R: " ~ toString(rMin)
            ~ " | Height: " ~ toString(toothHeight)
            ~ " | Samples: " ~ toString(size(sampleData)) ~ "/" ~ toString(size(allSampleData))
            ~ " | Angle: " ~ toString(angleMin * 180 / PI) ~ "-" ~ toString(angleMax * 180 / PI) ~ " (" ~ toString(angleRangeDeg) ~ "deg)"
            ~ " | Vertices: " ~ toString(size(vertexRadii))
            ~ " | VMaxR: " ~ toString(vertexMaxR);

        reportFeatureInfo(context, id, diagMsg);
        println(diagMsg);

        if (definition.rename && teeth > 0)
            setProperty(context, { "entities" : definition.part, "propertyName" : PropertyType.NAME, "value" : definition.namePrefix ~ " (" ~ toString(teeth) ~ "T)" });
    });
