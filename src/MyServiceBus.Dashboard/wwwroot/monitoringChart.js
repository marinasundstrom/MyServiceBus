import * as d3 from "https://cdn.jsdelivr.net/npm/d3@7/+esm";

const colors = ["#55d6a9", "#69a8ff", "#f2bd5c", "#d68cff", "#ff837a", "#66d7e8"];

export function renderThroughputChart(element, rawPoints) {
    const points = rawPoints.map(point => ({
        application: point.applicationName,
        timestamp: new Date(point.timestampUtc),
        value: point.consumedPerSecond
    }));
    const series = d3.groups(points, point => point.application)
        .sort(([left], [right]) => d3.ascending(left, right));

    const root = d3.select(element);
    root.selectAll("*").remove();
    if (series.length === 0) {
        root.append("p").attr("class", "panel-empty").text("Waiting for throughput samples.");
        return;
    }

    const legend = root.append("div").attr("class", "chart-legend");
    series.forEach(([name, values], index) => {
        const item = legend.append("span");
        item.append("i").style("background", colors[index % colors.length]);
        item.append("span").text(name);
        item.append("strong").text(`${d3.format(".2f")(values.at(-1).value)} / sec`);
    });

    const width = 800;
    const height = 230;
    const margin = { top: 18, right: 16, bottom: 34, left: 50 };
    const timestamps = points.map(point => point.timestamp);
    const x = d3.scaleUtc()
        .domain(d3.extent(timestamps))
        .range([margin.left, width - margin.right]);
    const y = d3.scaleLinear()
        .domain([0, Math.max(1, d3.max(points, point => point.value))])
        .nice()
        .range([height - margin.bottom, margin.top]);
    const line = d3.line()
        .x(point => x(point.timestamp))
        .y(point => y(point.value))
        .curve(d3.curveMonotoneX);

    const svg = root.append("svg")
        .attr("viewBox", `0 0 ${width} ${height}`)
        .attr("role", "img")
        .attr("aria-label", "Consumed messages per second by application");

    svg.append("g")
        .attr("class", "chart-grid")
        .attr("transform", `translate(${margin.left},0)`)
        .call(d3.axisLeft(y).ticks(4).tickSize(-(width - margin.left - margin.right)).tickFormat(""));
    svg.append("g")
        .attr("class", "chart-axis")
        .attr("transform", `translate(0,${height - margin.bottom})`)
        .call(d3.axisBottom(x).ticks(5).tickFormat(d3.utcFormat("%H:%M:%S")));
    svg.append("g")
        .attr("class", "chart-axis")
        .attr("transform", `translate(${margin.left},0)`)
        .call(d3.axisLeft(y).ticks(4));

    series.forEach(([, values], index) => {
        svg.append("path")
            .datum(values)
            .attr("class", "chart-line")
            .attr("stroke", colors[index % colors.length])
            .attr("d", line);
    });
}
