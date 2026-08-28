export default function HeroMessagingDiagram() {
  return (
    <svg
      aria-hidden="true"
      className="hero-network"
      focusable="false"
      viewBox="0 0 720 190"
    >
      <g className="network-dots">
        {Array.from({ length: 12 }, (_, index) => (
          <circle
            cx={18 + (index % 4) * 14}
            cy={18 + Math.floor(index / 4) * 14}
            key={index}
            r="2.5"
          />
        ))}
      </g>

      <path className="network-corner" d="M78 61V35h26" />

      <path className="network-route" d="M18 132h72c22 0 19-29 43-29h22" />
      <path className="network-route" d="M90 132c22 0 19 29 43 29h22" />
      <circle className="network-signal" cx="18" cy="132" r="9" />
      <circle className="network-mint" cx="155" cy="103" r="10" />
      <circle className="network-outline" cx="155" cy="161" r="10" />

      <circle className="network-signal" cx="282" cy="63" r="9" />
      <path className="network-route" d="M291 63h89" />
      <circle className="network-envelope-node" cx="423" cy="63" r="42" />
      <path className="network-icon" d="M401 50h44v28h-44z" />
      <path className="network-icon" d="m402 52 21 16 21-16" />
      <path className="network-route" d="M465 63h38c28 0 17-38 45-38h39" />
      <circle className="network-mint" cx="597" cy="25" r="10" />

      <path className="network-route network-route-dashed" d="M465 63h108" />
      <circle className="network-outline" cx="579" cy="63" r="5" />
      <path className="network-route network-route-dashed" d="M584 63c31 0 21 42 48 42h39" />
      <circle className="network-signal" cx="680" cy="105" r="11" />

      <g className="network-signal-dots">
        <circle cx="222" cy="166" r="3" />
        <circle cx="240" cy="166" r="3" />
        <circle cx="258" cy="166" r="3" />
      </g>

      <circle className="network-signal" cx="472" cy="156" r="16" />
      <g className="network-message-dots">
        <circle cx="466" cy="156" r="2" />
        <circle cx="472" cy="156" r="2" />
        <circle cx="478" cy="156" r="2" />
      </g>
      <path className="network-route network-route-dashed" d="M488 156h38c24 0 18-24 42-24h12" />
      <rect className="network-message-node" height="42" rx="8" width="77" x="580" y="111" />
      <path className="network-icon" d="M594 123h48M594 132h48M594 141h35" />
      <path className="network-route" d="M657 132h18c14 0 14 12 14 24v34" />

      <path className="network-corner" d="M700 150v26h-26" />
    </svg>
  );
}
