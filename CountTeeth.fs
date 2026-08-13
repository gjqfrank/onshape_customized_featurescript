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

        var endFace = endFaces[0];
        var endPlane = evPlane(context, { "face" : endFace });
        var endZ = dot(endPlane.origin - axisOrigin, axisDir);

        // ---------- 3. 取端面上的所有边(按中点轴向坐标筛选) -----------------
        var allEdges = evaluateQuery(context, qOwnedByBody(definition.part, EntityType.EDGE));

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
            throw "端面上未找到齿廓边。";

        // ---------- 4. 按连通环分组, 选半径最大的环(齿顶外环) ---------------
        var processed = new box([]);
        var loops = []; // 每个元素是边数组
        for (var edge in profileEdges)
        {
            var already = false;
            for (var e in processed[])
            {
                if (e == edge)
                {
                    already = true;
                    break;
                }
            }
            if (already)
                continue;

            var loopEdges;
            try silent
            {
                loopEdges = evaluateQuery(context, qLoopEdges(edge));
            }
            if (loopEdges == undefined || size(loopEdges) == 0)
                loopEdges = [edge];

            loops = append(loops, loopEdges);
            for (var e in loopEdges)
                processed[] = append(processed[], e);
        }

        // 选半径最大的环
        var outerLoop = loops[0];
        var outerMaxR = 0 * millimeter;
        for (var loop in loops)
        {
            var maxR = 0 * millimeter;
            for (var edge in loop)
            {
                try silent
                {
                    var pt = evEdgeTangentLine(context, { "edge" : edge, "parameter" : 0.5 }).origin;
                    var r = radialDistance(pt, axisOrigin, axisDir);
                    if (r > maxR)
                        maxR = r;
                }
            }
            if (maxR > outerMaxR)
            {
                outerMaxR = maxR;
                outerLoop = loop;
            }
        }

        // 绿色高亮齿顶外环
        for (var e in outerLoop)
            addDebugEntities(context, e, DebugColor.GREEN);

        // ---------- 5. 沿外环密集采样, 计算每个点的角度和半径 ----------------
        // 建立垂直于轴的参考坐标系
        var refU;
        if (abs(axisDir[0]) < 0.9)
            refU = normalize(cross(axisDir, vector(1, 0, 0)));
        else
            refU = normalize(cross(axisDir, vector(0, 1, 0)));
        var refV = cross(axisDir, refU);

        var sampleData = []; // { "angle", "radius", "point" }
        var samplesPerEdge = size(outerLoop) == 1 ? 360 : 20;

        for (var edge in outerLoop)
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
            throw "采样点不足，无法分析齿廓。";

        // ---------- 6. 按角度排序, 提取半径序列 ------------------------------
        // 选择排序(样本量 ≤ 360, O(n²) 可接受)
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

        // ---------- 7. 计算阈值并数齿 ---------------------------------------
        var rMax = samples[0];
        var rMin = samples[0];
        for (var s in samples)
        {
            if (s > rMax) rMax = s;
            if (s < rMin) rMin = s;
        }

        var toothHeight = rMax - rMin;
        var isSmooth = (rMax <= 0 * millimeter) || (toothHeight / rMax < 0.005);

        var tipThreshold = rMin + toothHeight * 0.7;
        var teeth = isSmooth ? 0 : countPeaksAbove(samples, tipThreshold);

        // 红色标出齿顶采样点(即使齿数为0也标, 便于诊断)
        if (!isSmooth)
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
        // 诊断信息: 选中环的边数、采样点数、齿高
        var diagMsg = "Teeth: " ~ toString(teeth)
            ~ " | Tip R: " ~ toString(rMax)
            ~ " | Root R: " ~ toString(rMin)
            ~ " | Height: " ~ toString(toothHeight)
            ~ " | Samples: " ~ toString(size(sampleData))
            ~ " | Loop edges: " ~ toString(size(outerLoop));

        // 1) computed parameter —— 显示在特征对话框顶部
        setFeatureComputedParameter(context, id, {
            "parameterId" : "computedToothCount",
            "parameterName" : "Tooth count",
            "format" : { "formatString" : "#", "units" : "" },
            "value" : teeth,
            "rememberIfDefault" : false
        });

        setFeatureComputedParameter(context, id, {
            "parameterId" : "computedTipR",
            "parameterName" : "Tip radius",
            "format" : { "formatString" : "#.###", "units" : "mm" },
            "value" : rMax,
            "rememberIfDefault" : false
        });

        setFeatureComputedParameter(context, id, {
            "parameterId" : "computedRootR",
            "parameterName" : "Root radius",
            "format" : { "formatString" : "#.###", "units" : "mm" },
            "value" : rMin,
            "rememberIfDefault" : false
        });

        setFeatureComputedParameter(context, id, {
            "parameterId" : "computedHeight",
            "parameterName" : "Tooth height",
            "format" : { "formatString" : "#.###", "units" : "mm" },
            "value" : toothHeight,
            "rememberIfDefault" : false
        });

        // 2) reportFeatureInfo —— 特征树悬停显示诊断信息
        reportFeatureInfo(context, id, diagMsg);

        // 3) 控制台输出
        println(diagMsg);

        // 4) 可选: 重命名零件(默认关闭)
        if (definition.rename && teeth > 0)
            setProperty(context, { "entities" : definition.part, "propertyName" : PropertyType.NAME, "value" : definition.namePrefix ~ " (" ~ toString(teeth) ~ "T)" });
    });
