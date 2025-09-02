package com.myservicebus;

import com.fasterxml.jackson.annotation.JsonProperty;
import lombok.AllArgsConstructor;
import lombok.Data;
import lombok.NoArgsConstructor;

@Data
@NoArgsConstructor
@AllArgsConstructor
public class HostInfo {

    @JsonProperty("machineName")
    private String machineName;

    @JsonProperty("processName")
    private String processName;

    @JsonProperty("processId")
    private int processId;

    @JsonProperty("assembly")
    private String assembly;

    @JsonProperty("assemblyVersion")
    private String assemblyVersion;

    @JsonProperty("frameworkVersion")
    private String frameworkVersion;

    @JsonProperty("massTransitVersion")
    private String massTransitVersion;

    @JsonProperty("operatingSystemVersion")
    private String operatingSystemVersion;
}
