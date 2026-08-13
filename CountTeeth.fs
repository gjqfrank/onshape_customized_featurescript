FeatureScript 2200;

// 若 Feature Studio 提示版本不匹配，点击编辑器中的灯泡即可更新到当前工作室的版本。
import(path : "onshape/std/geometry.fs", version : "2200.0");

// ===========================================================================
// Count Teeth —— 识别带轮(pulley) / 链轮(sprocket) / 齿轮(gear) 的齿数
//
// 原理:
//   这类零件通常是"齿廓草图沿轴拉伸"的平板件, 两个端面(⊥轴的平面)的外边界
//   就是完整齿廓。本特征:
//     1. 由用户指定回转轴(圆柱面或圆边);
//     2. 找到垂直于轴的平面端面;
//     3. 取该端面所有边, 按连通环分组, 选半径最大的环 = 齿顶外环;
//     4. 沿外环边密集采样(evEdgeTangentLine, 不依赖顶点, 兼容周期样条);
//     5. 按角度排序, 以"齿根半径 + 0.7×齿高"为阈值统计径向峰值 = 齿数。
//   兼容: 单条周期样条齿廓、多段圆弧/直线齿廓、齿顶倒圆、齿根弧形。
// ===========================================================================

// 点到轴线的径向距离
function radialDistance(point is Vector, axisOrigin is Vector, axisDir is Vector) returns ValueWithUnits
{
    var d = point - axisOrigin;
    return norm(d - dot(d, axisDir) * axisDir);
}

// 在周期性半径序列中统计越过阈值的上升沿数量(=齿数)
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

        annotation { "Name" : "Axis (cylindrical face or circular edge)", "Filter" : (EntityType.FACE && GeometryType.CYLINDER) || (EntityType.EDGE && GeometryType.CIRCLE), "MaxNumberOfPicks" : 1 }
        definition.axis is Query;

        annotation { "Name" : "Rename part using tooth count", "Default" : false }
        definition.rename is boolean;

        annotation { "Name" : "Name prefix", "Default" : "Pulley" }
        definition.namePrefix is string;
    }
    {
        // ---------- 1. 解析回转轴 --------------------------------------------
        var ax = evAxis(context, { "axis" : definition.axis });
        var axisOrigin is Vector = ax.origin;
        var axisDir is Vector = ax.direction;

        // ---------- 2. 找到垂直于轴的平面端面 --------------------------------
        var allFaces = evaluateQuery(context, qOwnedByBody(definition.part, EntityType.FACE));
        var endFaces = [];
        for (var face in allFaces)
        {
            try silent
            {
                var plane = evPlane(context, { "face" : face });
                if (abs(dot(plane.normal, axisDir)) > 0.999)
                    endFaces = append(endFaces, face);
            }
        }

        if (size(endFaces) == 0)
            throw "未找到垂直于轴的平面端面。请确认所选零件是带轮或链轮。";

        // 收集所有边, 按轴向坐标缓存中点半径(一次性计算, 避免重复)
        var allEdges = evaluateQuery(context, qOwnedByBody(definition.part, EntityType.EDGE));

        // ---------- 3~7. 遍历每个端面, 每个环, 选齿高最大的作为齿廓 ------------
        // (旧代码只取 endFaces[0] 的最大半径环, 若该端面是光滑圆则齿高=0 直接失败)
        var refU;
        if (abs(axisDir[0]) < 0.9)
            refU = normalize(cross(axisDir, vector(1, 0, 0)));
        else
            refU = normalize(cross(axisDir, vector(0, 1, 0)));
        var refV = cross(axisDir, refU);

        var bestResult = undefined; // { teeth, rMax, rMin, height, samples, loopEdges, loopCount, sampleData }

        for (var endFace in endFaces)
        {
            var endPlane = evPlane(context, { "face" : endFace });
            var endZ = dot(endPlane.origin - axisOrigin, axisDir);

            // 该端面上的边
            var profileEdges = [];
            for (var edge in allEdges)
            {
                try silent
                {
                    var pt = evEdgeTangentLine(context, { "edge" : edge, "parameter" : 0.5 }).origin;
                    var z = dot(pt - axisOrigin, axisDir);
                    if (abs(z - endZ) < 1e-3 * millimeter)
                        profileEdges = append(profileEdges, edge);
                }
            }
            if (size(profileEdges) == 0)
                continue;

            // 分环
            var processed = new box([]);
            var loops = [];
            for (var edge in profileEdges)
            {
                var already = false;
                for (var e in processed[])
                {
                    if (e == edge) { already = true; break; }
                }
                if (already) continue;

                var loopEdges;
                try silent { loopEdges = evaluateQuery(context, qLoopEdges(edge)); }
                if (loopEdges == undefined || size(loopEdges) == 0)
                    loopEdges = [edge];

                loops = append(loops, loopEdges);
                for (var e in loopEdges)
                    processed[] = append(processed[], e);
            }

            // 对每个环采样并计算齿高, 选齿高最大的环
            for (var loop in loops)
            {
                var sampleData = [];
                var samplesPerEdge = size(loop) == 1 ? 360 : 20;

                for (var edge in loop)
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
                            var du = dot(proj, refU);
                            var dv = dot(proj, refV);
                            var angle = atan2(dv, du);
                            sampleData = append(sampleData, { "angle" : angle, "radius" : r, "point" : pt });
                        }
                    }
                }
                if (size(sampleData) < 4)
                    continue;

                // 按角度排序
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

                var rMax = samples[0];
                var rMin = samples[0];
                for (var s in samples)
                {
                    if (s > rMax) rMax = s;
                    if (s < rMin) rMin = s;
                }
                var height = rMax - rMin;

                // 选齿高最大的环(齿廓), 而非半径最大的环(可能是光滑圆)
                if (bestResult == undefined || height > bestResult.height)
                {
                    var tipThreshold = rMin + height * 0.7;
                    var teeth = (height / rMax < 0.005) ? 0 : countPeaksAbove(samples, tipThreshold);
                    bestResult = {
                        "teeth" : teeth,
                        "rMax" : rMax,
                        "rMin" : rMin,
                        "height" : height,
                        "samples" : samples,
                        "sampleData" : sampleData,
                        "loopEdges" : loop,
                        "loopCount" : size(loop),
                        "tipThreshold" : tipThreshold
                    };
                }
            }
        }

        if (bestResult == undefined)
            throw "未在任何端面上找到可分析的边环。";

        var teeth = bestResult.teeth;
        var rMax = bestResult.rMax;
        var rMin = bestResult.rMin;
        var toothHeight = bestResult.height;
        var sampleData = bestResult.sampleData;
        var outerLoop = bestResult.loopEdges;
        var tipThreshold = bestResult.tipThreshold;

        // 绿色高亮选中的齿廓环
        for (var e in outerLoop)
            addDebugEntities(context, e, DebugColor.GREEN);

        // 红色标出齿顶采样点(齿数>0时才标)
        if (teeth > 0)
        {
            for (var sd in sampleData)
            {
                if (sd.radius >= tipThreshold)
                {
                    try silent
                    {
                        debug(context, sd.point, DebugColor.RED);
                    }
                }
            }
        }

        // ---------- 8. 输出结果(永远显示, 不静默返回) ----------------------
        // 诊断信息
        var diagMsg = "Teeth: " ~ toString(teeth)
            ~ " | Tip R: " ~ toString(rMax)
            ~ " | Root R: " ~ toString(rMin)
            ~ " | Height: " ~ toString(toothHeight)
            ~ " | Samples: " ~ toString(size(sampleData))
            ~ " | Loop edges: " ~ toString(size(outerLoop));

        // 1) reportFeatureInfo —— 特征上显示蓝色 ℹ️ 图标, 悬停可见齿数
        reportFeatureInfo(context, id, diagMsg);

        // 2) 控制台输出
        println(diagMsg);

        // 3) 可选: 重命名零件(默认关闭)
        if (definition.rename && teeth > 0)
            setProperty(context, { "entities" : definition.part, "propertyName" : PropertyType.NAME, "value" : definition.namePrefix ~ " (" ~ toString(teeth) ~ "T)" });
    });
