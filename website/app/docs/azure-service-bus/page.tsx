import Link from 'next/link';
import CodeViewer from '../../components/CodeViewer';
import LanguageTabs from '../../components/LanguageTabs';

const provision = `resource_group="rg-myservicebus"
namespace_name="<globally-unique-namespace>"
location="swedencentral"

az login
az group create \\
  --name "$resource_group" \\
  --location "$location"
az servicebus namespace create \\
  --resource-group "$resource_group" \\
  --name "$namespace_name" \\
  --location "$location" \\
  --sku Standard
az servicebus namespace authorization-rule create \\
  --resource-group "$resource_group" \\
  --namespace-name "$namespace_name" \\
  --name MyServiceBus \\
  --rights Manage Send Listen`;

const connectionString = `export MY_SERVICE_BUS_CONNECTION_STRING="$(
  az servicebus namespace authorization-rule keys list \\
    --resource-group "$resource_group" \\
    --namespace-name "$namespace_name" \\
    --name MyServiceBus \\
    --query primaryConnectionString \\
    --output tsv
)"`;

const install = {
  csharp: `dotnet add package Sundstrom.MyServiceBus.AzureServiceBus \\
  --version 0.1.0-preview.1`,
  java: `dependencies {
    implementation 'io.github.marinasundstrom.myservicebus:myservicebus-azure-service-bus:0.1.0-preview.1'
}`,
};

const configure = {
  csharp: `var builder = WebApplication.CreateBuilder(args);

builder.Services.AddServiceBus(x =>
{
    x.AddConsumer<SubmitOrderConsumer>();

    x.UsingAzureServiceBus((context, cfg) =>
    {
        cfg.Host(builder.Configuration["MY_SERVICE_BUS_CONNECTION_STRING"]!);
        cfg.ConfigureEndpoints(context);
    });
});

var app = builder.Build();
await app.StartAsync();`,
  java: `String connectionString =
    System.getenv("MY_SERVICE_BUS_CONNECTION_STRING");

ServiceCollection services = ServiceCollection.create();
services.from(MessageBusServices.class)
    .addServiceBus(cfg -> {
        cfg.addConsumer(SubmitOrderConsumer.class);
        cfg.using(AzureServiceBusFactoryConfigurator.class,
            (context, serviceBus) -> {
                serviceBus.host(connectionString);
                serviceBus.configureEndpoints(context);
            });
    });

ServiceProvider provider = services.buildServiceProvider();
MessageBus bus = provider.getService(MessageBus.class);
bus.start().join();`,
};

const preProvisioned = {
  csharp: `cfg.Host(connectionString);
cfg.UsePreProvisionedTopology();
cfg.ConfigureEndpoints(context);`,
  java: `serviceBus.host(connectionString);
serviceBus.usePreProvisionedTopology();
serviceBus.configureEndpoints(context);`,
};

const teardown = `az group delete \\
  --name "$resource_group" \\
  --yes`;

function ShellBlock({ code, label }: { code: string; label: string }) {
  return (
    <div className="docs-code-block">
      <div className="docs-code-toolbar"><span>{label}</span></div>
      <CodeViewer code={code} label={label} language="shell" />
    </div>
  );
}

export default function AzureServiceBus() {
  return (
    <article className="docs-article">
      <p className="docs-kicker">Azure Service Bus transport</p>
      <h1>Run MyServiceBus on Azure Service Bus.</h1>
      <p className="docs-summary">Provision a namespace, connect the C# or Java client, and let MyServiceBus create the queues, topics, and subscriptions needed by your consumers.</p>

      <div className="callout callout-accent"><strong>Experimental preview</strong><p>The transport is ready for evaluation against live Azure, but is not yet part of the supported interoperability baseline. Preview releases may make breaking configuration changes.</p></div>

      <h2>1. Provision a namespace</h2>
      <p>Install the Azure CLI, sign in, and create an isolated Service Bus namespace. Replace the namespace placeholder with a globally unique name. Use Standard or Premium: the Basic tier does not support the topics and subscriptions required for publish/subscribe.</p>
      <ShellBlock code={provision} label="AZURE CLI" />
      <p>The default <code>Create</code> topology mode uses the administration API, so this first setup grants the connection <code>Manage</code>, <code>Send</code>, and <code>Listen</code>. MyServiceBus then provisions entities when the bus starts.</p>

      <h2>2. Read the connection string safely</h2>
      <p>Load the namespace connection string into your deployment’s secret store or an environment variable. Do not commit it to source control or put it in a client-side configuration file.</p>
      <ShellBlock code={connectionString} label="CURRENT SHELL" />
      <div className="callout"><strong>Authentication in this preview</strong><p>The current transport configuration accepts an Azure Service Bus SAS connection string. Managed identity and <code>TokenCredential</code>-based configuration are not exposed yet.</p></div>

      <h2>3. Install the transport</h2>
      <p>The transport package brings in the corresponding MyServiceBus runtime and Azure SDK dependencies.</p>
      <LanguageTabs csharp={install.csharp} java={install.java} csharpLabel=".NET CLI" javaLabel="Gradle" csharpLanguage="shell" javaLanguage="groovy" />

      <h2>4. Configure the bus</h2>
      <p>Register consumers through the same factory shape used by the RabbitMQ transport. In the default mode, <code>ConfigureEndpoints</code> creates each consumer endpoint and its publish subscription.</p>
      <LanguageTabs csharp={configure.csharp} java={configure.java} />

      <h2>Topology created at startup</h2>
      <p>For a consumer endpoint named <code>orders</code>, MyServiceBus creates a queue, a topic for each consumed message contract, and a subscription that forwards messages into the queue. Failure companions follow the same conventions used for MassTransit interoperability.</p>
      <div className="queue-map">
        <div><span>orders</span><p>Primary peek-lock delivery queue</p></div>
        <div><span>orders_error</span><p>Original messages after terminal failure</p></div>
        <div><span>orders_fault</span><p>Published <code>Fault&lt;T&gt;</code> details</p></div>
        <div><span>orders_skipped</span><p>Messages with no recognized consumer</p></div>
      </div>

      <h2>Infrastructure-managed topology</h2>
      <p>If Terraform, Bicep, or another deployment system owns the entities, switch both clients to <code>PreProvisioned</code>. The application will use the configured queues, topics, subscriptions, and failure companions without trying to create them. Give that runtime connection only the <code>Send</code> and <code>Listen</code> rights it needs.</p>
      <LanguageTabs csharp={preProvisioned.csharp} java={preProvisioned.java} />
      <p className="small-note">Request clients normally create unique auto-delete response queues. A pre-provisioned environment must also map temporary response endpoint names to infrastructure-owned queues.</p>

      <h2>MassTransit interoperability</h2>
      <p>Azure entity names are part of the wire contract. Keep corresponding C# and Java message contracts aligned, and use the same explicit entity-name overrides in MyServiceBus and MassTransit when defaults are not suitable. Live tests cover directed sends, default-named publication, correlated responses, and correlated faults in every direction for both clients.</p>

      <h2>Remove an evaluation environment</h2>
      <p>If the resource group is dedicated to this evaluation, delete it when you finish to stop further Azure charges. This removes the namespace and every entity inside it.</p>
      <ShellBlock code={teardown} label="TEAR DOWN" />

      <p className="small-note">Azure CLI command details and Service Bus tier behavior are documented by <a href="https://learn.microsoft.com/cli/azure/servicebus/namespace?view=azure-cli-latest">Microsoft Learn</a>.</p>

      <div className="next-card"><div><span>Compatibility</span><strong>See the verified baseline and remaining preview gates</strong></div><Link href="/docs/interoperability">Interoperability →</Link></div>
    </article>
  );
}
