﻿namespace Nett
{
    using System.Diagnostics;
    using Extensions;

    [DebuggerDisplay("{Value}")]
    public sealed class TomlString : TomlValue<string>
    {
        private readonly TypeOfString type = TypeOfString.Default;

        internal TomlString(ITomlRoot root, string value, TypeOfString type = TypeOfString.Default)
            : base(root, value)
        {
            this.type = type;
        }

        public enum TypeOfString
        {
            Default,
            Normal,
            Literal,
            Multiline,
            MultilineLiteral,
        }

        public override string ReadableTypeName => "string";

        public override TomlObjectType TomlType => TomlObjectType.String;

        public override void Visit(ITomlObjectVisitor visitor)
        {
            visitor.Visit(this);
        }

        internal override TomlValue ValueWithRoot(ITomlRoot root) => this.StringWithRoot(root);

        internal override TomlObject WithRoot(ITomlRoot root) => this.StringWithRoot(root);

        internal TomlString StringWithRoot(ITomlRoot root)
        {
            root.CheckNotNull(nameof(root));

            return new TomlString(root, this.Value, this.type);
        }

        internal TomlString CloneStringFor(TomlObject newParent)
        {
            return new TomlString(this.Root, this.Value) { parent = newParent };
        }

        internal override TomlObject CloneFor(TomlObject newParent) => this.CloneStringFor(newParent);
    }
}
