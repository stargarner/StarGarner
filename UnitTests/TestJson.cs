#nullable enable
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using StarGarner.Util;
using System;

namespace StarGarner {

    [TestClass]
    public class TestJson {

        [TestMethod]
        public void TestMethodNullable() {
            var root = new JObject();

            Assert.IsNull( root.Value<Boolean?>( "a" ) );
            Assert.IsNull( root.Value<Int32?>( "a" ) );
            Assert.IsNull( root.Value<Int64?>( "a" ) );
            Assert.IsNull( root.Value<String?>( "a" ) );
            Assert.IsNull( root.Value<JArray?>( "a" ) );

        }


        [TestMethod]
        public void TestMethodNonNull() {
            var root = new JObject();

            Assert.AreEqual( false, root.Value<Boolean>( "a" ) );
            Assert.AreEqual( 0, root.Value<Int32>( "a" ) );
            Assert.AreEqual( 0L, root.Value<Int64>( "a" ) );
            Assert.IsNull( root.Value<String>( "a" ) );
            Assert.IsNull( root.Value<JArray>( "a" ) );
        }

        [TestMethod]
        public void TestMethodNullableValuteType() {
            var root = new JObject() {
                { "b",true },
                {  "i",1},
                {"l",1L },
                {"s","s" },
                {"a" ,new JArray()}
            };

            Assert.IsInstanceOfType( root.Value<Boolean?>( "b" ), typeof( Boolean ) );
            Assert.IsInstanceOfType( root.Value<Int32?>( "i" ), typeof( Int32 ) );
            Assert.IsInstanceOfType( root.Value<Int64?>( "l" ), typeof( Int64 ) );
            Assert.IsInstanceOfType( root.Value<String?>( "s" ), typeof( String ) );
            Assert.IsInstanceOfType( root.Value<JArray?>( "a" ), typeof( JArray ) );
        }


        [TestMethod]
        public void TestMethodNullableValuteType2() {
            var root = new JObject() {
                { "b",true },
                {  "i",1},
                {"l",1L },
                {"s","s" },
                {"a" ,new JArray()}
            };

            Assert.AreEqual( root.Value<Boolean?>( "b" ), true );
            Assert.AreEqual( root.Value<Int32?>( "i" ), 1 );
            Assert.AreEqual( root.Value<Int64?>( "l" ), 1L );
        }
    }
}
